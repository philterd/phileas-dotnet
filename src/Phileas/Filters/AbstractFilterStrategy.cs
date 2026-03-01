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

using Phileas.Model.Filtering;
using Phileas.Policy;

namespace Phileas.Filters;

/// <summary>
/// Base class for all runtime filter strategies. Defines the replacement-strategy constants,
/// per-strategy configuration properties, and the abstract methods that concrete strategies must implement.
/// </summary>
public abstract class AbstractFilterStrategy
{
    /// <summary>Replacement strategy constant: redact the entity with a format string.</summary>
    public const string Redact = "REDACT";
    /// <summary>Replacement strategy constant: replace with a consistent random value within a context.</summary>
    public const string RandomReplace = "RANDOM_REPLACE";
    /// <summary>Replacement strategy constant: replace with a configured static value.</summary>
    public const string StaticReplace = "STATIC_REPLACE";
    /// <summary>Replacement strategy constant: encrypt using AES with the policy's crypto settings.</summary>
    public const string CryptoReplace = "CRYPTO_REPLACE";
    /// <summary>Replacement strategy constant: encrypt using format-preserving encryption (FPE).</summary>
    public const string FpeEncryptReplace = "FPE_ENCRYPT_REPLACE";
    /// <summary>Replacement strategy constant: replace with the SHA-256 hash of the entity.</summary>
    public const string HashSha256Replace = "HASH_SHA256_REPLACE";
    /// <summary>Replacement strategy constant: keep only the last four characters.</summary>
    public const string Last4 = "LAST_4";
    /// <summary>Replacement strategy constant: mask the entity with a repeated mask character.</summary>
    public const string Mask = "MASK";
    /// <summary>Replacement strategy constant: keep the original entity unchanged.</summary>
    public const string Same = "SAME";
    /// <summary>Replacement strategy constant: keep only the first character.</summary>
    public const string Truncate = "TRUNCATE";
    /// <summary>Default redaction format string; <c>%t</c> is replaced with the filter-type slug.</summary>
    public const string DefaultRedaction = "{{{REDACTED-%t}}}";

    /// <summary>Gets or sets the replacement strategy name. Defaults to <see cref="Redact"/>.</summary>
    public string Strategy { get; set; } = Redact;

    /// <summary>Gets or sets the redaction format string used by the <see cref="Redact"/> strategy. Defaults to <see cref="DefaultRedaction"/>.</summary>
    public string RedactionFormat { get; set; } = DefaultRedaction;

    /// <summary>Gets or sets the static replacement value used by the <see cref="StaticReplace"/> strategy.</summary>
    public string StaticReplacement { get; set; } = string.Empty;

    /// <summary>Gets or sets the character used for masking when the <see cref="Mask"/> strategy is active. Defaults to <c>"*"</c>.</summary>
    public string MaskCharacter { get; set; } = "*";

    /// <summary>Gets or sets the mask length for the <see cref="Mask"/> strategy. Use <c>"same"</c> to match the entity length, or a numeric string for a fixed length.</summary>
    public string MaskLength { get; set; } = "same";

    /// <summary>Gets or sets an optional filter condition expression. When <see langword="null"/> or empty the strategy always applies.</summary>
    public string? Condition { get; set; }

    /// <summary>Gets or sets a value indicating whether a random cryptographic salt is appended before hashing. Defaults to <see langword="false"/>.</summary>
    public bool Salt { get; set; } = false;

    /// <summary>Gets or sets the context service used to persist random replacements across calls. May be <see langword="null"/>.</summary>
    public IContextService? ContextService { get; set; }

    /// <summary>
    /// Computes the replacement value for a detected entity.
    /// </summary>
    /// <param name="context">The context identifier.</param>
    /// <param name="token">The original entity text.</param>
    /// <param name="window">Words surrounding the entity.</param>
    /// <param name="confidence">Confidence score of the detection.</param>
    /// <param name="classification">Optional entity classification label.</param>
    /// <param name="filterPattern">The pattern that produced the match.</param>
    /// <param name="crypto">AES crypto settings from the policy, or <see langword="null"/>.</param>
    /// <param name="fpe">Format-preserving encryption settings from the policy, or <see langword="null"/>.</param>
    /// <returns>A <see cref="Replacement"/> containing the replacement value and optional salt.</returns>
    public abstract Replacement GetReplacement(string context, string token, string[] window, double confidence, string? classification, FilterPattern? filterPattern, Crypto? crypto, Fpe? fpe);

    /// <summary>
    /// Evaluates whether this strategy's condition is satisfied for the given entity.
    /// </summary>
    /// <param name="context">The context identifier.</param>
    /// <param name="token">The original entity text.</param>
    /// <param name="window">Words surrounding the entity.</param>
    /// <param name="confidence">Confidence score of the detection.</param>
    /// <param name="classification">Optional entity classification label.</param>
    /// <param name="filterPattern">The pattern that produced the match.</param>
    /// <returns><see langword="true"/> if the strategy should be applied; otherwise <see langword="false"/>.</returns>
    public abstract bool EvaluateCondition(string context, string token, string[] window, double confidence, string? classification, FilterPattern? filterPattern);

    /// <summary>
    /// Produces a redacted token string by substituting the filter-type slug and optional label
    /// into <see cref="RedactionFormat"/>.
    /// </summary>
    /// <param name="token">The original entity text (unused in the default implementation).</param>
    /// <param name="label">Optional label to substitute for the <c>%l</c> placeholder.</param>
    /// <param name="filterType">The filter type whose slug replaces the <c>%t</c> placeholder.</param>
    /// <returns>The formatted redaction string.</returns>
    protected string GetRedactedToken(string token, string? label, FilterType filterType)
    {
        var format = RedactionFormat ?? DefaultRedaction;
        var result = format.Replace("%t", filterType.GetFilterTypeName());
        if (label != null)
            result = result.Replace("%l", label);
        return result;
    }
}
