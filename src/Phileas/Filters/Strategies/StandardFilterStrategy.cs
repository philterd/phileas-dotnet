/*
 * Copyright 2026 Philterd, LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Security.Cryptography;
using System.Text;
using Phileas.Filters.Conditions;
using Phileas.Model;
using Phileas.Policy;

namespace Phileas.Filters.Strategies;

/// <summary>
///     Concrete base strategy that implements <see cref="EvaluateCondition" /> using
///     <see cref="ConditionEvaluator" /> and provides <see cref="GetStandardReplacement" />
///     which dispatches to the correct algorithm based on <see cref="AbstractFilterStrategy.Strategy" />.
///     All type-specific filter strategies in the <c>Strategies.Rules</c> namespace inherit from this class.
/// </summary>
public abstract class StandardFilterStrategy : AbstractFilterStrategy
{
    /// <inheritdoc />
    public override bool EvaluateCondition(string context, string token, string[] window, double confidence,
        string? classification, FilterPattern? filterPattern)
    {
        if (string.IsNullOrEmpty(Condition)) return true;
        return ConditionEvaluator.Evaluate(Condition, context, token, confidence, classification);
    }

    /// <summary>
    ///     Computes the replacement value for the detected entity using the configured
    ///     <see cref="AbstractFilterStrategy.Strategy" />.
    /// </summary>
    /// <param name="context">The context identifier.</param>
    /// <param name="token">The original entity text.</param>
    /// <param name="window">Words surrounding the entity.</param>
    /// <param name="confidence">Confidence score of the detection.</param>
    /// <param name="classification">Optional entity classification label.</param>
    /// <param name="filterPattern">The pattern that produced the match.</param>
    /// <param name="crypto">AES crypto settings, or <see langword="null" />.</param>
    /// <param name="fpe">Format-preserving encryption settings, or <see langword="null" />.</param>
    /// <param name="filterType">The type of filter invoking this method (used for default redaction format).</param>
    /// <returns>A <see cref="Replacement" /> containing the replacement value and optional salt.</returns>
    protected Replacement GetStandardReplacement(string context, string token, string[] window, double confidence,
        string? classification, FilterPattern? filterPattern, Crypto? crypto, Fpe? fpe, FilterType filterType)
    {
        var salt = Salt ? GenerateSalt() : string.Empty;

        // The strategy that actually produces the replacement. For MAP_REPLACE this becomes the fallback
        // strategy when the token is absent from the lookup table and no generator produces a value; for
        // every other strategy it is simply the strategy itself.
        var effectiveStrategy = Strategy;

        if (string.Equals(Strategy, MapReplace, StringComparison.OrdinalIgnoreCase))
        {
            // In CONTEXT scope a token is replaced consistently: reuse the replacement resolved for it earlier
            // in the context so a repeated value yields the same output and the generator is not invoked again.
            // Both mapped and generated values route through this same cache, shared with RANDOM_REPLACE.
            var contextScope =
                string.Equals(ReplacementScope, ReplacementScopeContext, StringComparison.OrdinalIgnoreCase)
                && ContextService != null;

            if (contextScope)
            {
                var cached = ContextService!.Get(context, token);
                if (cached != null)
                    return new Replacement(cached, salt);
            }

            // Lookup table (inline mappings merged over any loaded from mappingFiles), then the generator. A
            // miss, or a generator failure/timeout/rejected output, returns null and falls through to the
            // fallback strategy so the detected value is never left in the clear.
            var resolved = ResolveMapReplacement(token, classification);
            if (resolved != null)
            {
                if (contextScope)
                    ContextService!.Put(context, token, resolved);
                return new Replacement(resolved, salt);
            }

            // Fallback. The fallback enum never includes MAP_REPLACE, but guard against recursion anyway so a
            // hand-written policy can never loop.
            effectiveStrategy = string.IsNullOrEmpty(FallbackStrategy) ? Redact : FallbackStrategy;
            if (string.Equals(effectiveStrategy, MapReplace, StringComparison.OrdinalIgnoreCase))
                effectiveStrategy = Redact;
        }

        return effectiveStrategy switch
        {
            Redact => new Replacement(GetRedactedToken(token, classification, filterType), salt),
            RandomReplace => new Replacement(GetOrCreateRandomReplacement(context, token), salt),
            StaticReplace => new Replacement(
                !string.IsNullOrEmpty(StaticReplacement)
                    ? StaticReplacement
                    : GetRedactedToken(token, classification, filterType), salt),
            Mask => new Replacement(MaskToken(token), salt),
            Abbreviate => new Replacement(AbbreviateToken(token), salt),
            Last4 => new Replacement(token.Length >= 4 ? token[^4..] : token, salt),
            HashSha256Replace => new Replacement(HashSha256(token + salt), salt),
            CryptoReplace => crypto != null
                ? new Replacement("{{" + Utils.Encryption.Encrypt(token, crypto) + "}}", salt)
                : new Replacement(GetRedactedToken(token, classification, filterType), salt),
            FpeEncryptReplace => fpe != null
                ? FpeReplacement(context, token, window, confidence, classification, filterPattern, filterType, fpe, salt)
                : new Replacement(GetRedactedToken(token, classification, filterType), salt),
            Same => new Replacement(token, salt, false),
            Truncate => new Replacement(token.Length > 0 ? token[..1] : token, salt),
            _ => new Replacement(GetRedactedToken(token, classification, filterType), salt)
        };
    }

    private static string AbbreviateToken(string token)
    {
        var words = token.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var initials = new StringBuilder(words.Length);
        foreach (var word in words)
            initials.Append(char.ToUpperInvariant(word[0]));
        return initials.ToString();
    }

    private string GetOrCreateRandomReplacement(string context, string token)
    {
        // Produce a realistic fake value via the anonymization service when one is wired in; otherwise
        // fall back to a UUID.
        string Generate() =>
            AnonymizationService != null ? AnonymizationService.Anonymize(token) : Guid.NewGuid().ToString();

        // CONTEXT scope: reuse a token's previously generated replacement so the same value is anonymized
        // consistently across the context (referential integrity). DOCUMENT scope (the default, matching
        // Java) does not consult the context and anonymizes each occurrence independently.
        if (string.Equals(ReplacementScope, ReplacementScopeContext, StringComparison.OrdinalIgnoreCase)
            && ContextService != null)
        {
            var existing = ContextService.Get(context, token);
            if (existing != null) return existing;
            var replacement = Generate();
            ContextService.Put(context, token, replacement);
            return replacement;
        }

        return Generate();
    }

    /// <summary>
    ///     Builds the resolved <c>MAP_REPLACE</c> lookup table by merging the inline <see cref="AbstractFilterStrategy.Mappings" />
    ///     over any entries provided from <paramref name="fileMappings" />, normalizing each key for the configured case
    ///     sensitivity. Called once when the filter is built.
    /// </summary>
    /// <param name="fileMappings">Entries loaded from <c>mappingFiles</c>, or <see langword="null" /> if none.</param>
    public void InitializeMappings(IDictionary<string, string>? fileMappings)
    {
        var resolved = new Dictionary<string, string>();

        if (fileMappings != null)
            foreach (var entry in fileMappings)
                resolved[NormalizeMappingKey(entry.Key)] = entry.Value;

        // Inline mappings override entries loaded from files.
        if (Mappings != null)
            foreach (var entry in Mappings)
                resolved[NormalizeMappingKey(entry.Key)] = entry.Value;

        ResolvedMappings = resolved;
    }

    /// <summary>
    ///     Looks up a detected token in the resolved <c>MAP_REPLACE</c> lookup table.
    /// </summary>
    /// <param name="token">The detected value.</param>
    /// <returns>The mapped replacement, or <see langword="null" /> if the token is absent from the table.</returns>
    private string? LookupMapping(string token)
    {
        // The lookup table is normally built by the filter service when the strategy is wired up; fall back
        // to the inline mappings only (single-threaded callers such as unit tests) if it was not.
        if (ResolvedMappings == null)
            InitializeMappings(null);

        return ResolvedMappings!.GetValueOrDefault(NormalizeMappingKey(token));
    }

    private string NormalizeMappingKey(string key)
    {
        return CaseSensitive ? key : key.ToLowerInvariant();
    }

    /// <summary>
    ///     Resolves a <c>MAP_REPLACE</c> replacement from the lookup table or the generator. Returns the mapped value
    ///     if the token is in the table; otherwise the generator's output if one is configured and it produces an
    ///     accepted value; otherwise <see langword="null" /> to signal that the caller should apply the fallback
    ///     strategy.
    /// </summary>
    private string? ResolveMapReplacement(string token, string? label)
    {
        var mapped = LookupMapping(token);
        if (mapped != null) return mapped;

        // Skip the generator while re-scanning a previously generated value: the re-scan runs the filter
        // pipeline over a candidate, and invoking a generator there would recurse.
        if (ReplacementGenerator != null && !IsRescanning)
        {
            try
            {
                var generated = ReplacementGenerator.Generate(token, label);
                if (IsAcceptableGeneratedValue(token, generated))
                    return generated;
            }
            catch (Exception)
            {
                // The token is not logged. The caller applies the fallback strategy.
            }
        }

        return null;
    }

    /// <summary>
    ///     Whether a generated <c>MAP_REPLACE</c> value may be used: it must be non-blank, must not simply repeat the
    ///     original token (case-insensitively), and must not reintroduce detectable PII (re-scanned via
    ///     <see cref="AbstractFilterStrategy.ReplacementValidator" />).
    /// </summary>
    private bool IsAcceptableGeneratedValue(string token, string? generated)
    {
        if (string.IsNullOrWhiteSpace(generated))
            return false;

        // Reject a replacement that is just the original value again (case-insensitively), which would leave
        // the token effectively unredacted.
        if (string.Equals(generated.Trim(), token.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        // Re-scan the generated value to confirm the generator did not reintroduce PII.
        if (ReplacementValidator != null && ReplacementValidator.ContainsPii(generated))
            return false;

        return true;
    }

    private string MaskToken(string token)
    {
        if (MaskLength == "same") return new string(MaskCharacter[0], token.Length);
        if (int.TryParse(MaskLength, out var len)) return new string(MaskCharacter[0], Math.Min(len, token.Length));
        return new string(MaskCharacter[0], token.Length);
    }

    private static string GenerateSalt()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }

    private Replacement FpeReplacement(string context, string token, string[] window, double confidence,
        string? classification, FilterPattern? filterPattern, FilterType filterType, Fpe fpe, string salt)
    {
        try
        {
            return new Replacement(Utils.Encryption.FormatPreservingEncrypt(fpe, token), salt);
        }
        catch (Exception)
        {
            // The value cannot be format-preserving encrypted (for example its format-preservable content
            // is outside FF3's supported length range); fall back to redaction, matching the Java reference.
            return new Replacement(GetRedactedToken(token, classification, filterType), salt);
        }
    }
}
