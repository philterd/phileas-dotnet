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
using Phileas.Conditions;
using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;

namespace Phileas.Strategies;

/// <summary>
/// Concrete base strategy that implements <see cref="EvaluateCondition"/> using
/// <see cref="ConditionEvaluator"/> and provides <see cref="GetStandardReplacement"/>
/// which dispatches to the correct algorithm based on <see cref="AbstractFilterStrategy.Strategy"/>.
/// All type-specific filter strategies in the <c>Strategies.Rules</c> namespace inherit from this class.
/// </summary>
public abstract class StandardFilterStrategy : AbstractFilterStrategy
{
    /// <inheritdoc/>
    public override bool EvaluateCondition(string context, string token, string[] window, double confidence, string? classification, FilterPattern? filterPattern)
    {
        if (string.IsNullOrEmpty(Condition)) return true;
        return ConditionEvaluator.Evaluate(Condition, context, token, confidence, classification);
    }

    /// <summary>
    /// Computes the replacement value for the detected entity using the configured <see cref="AbstractFilterStrategy.Strategy"/>.
    /// </summary>
    /// <param name="context">The context identifier.</param>
    /// <param name="token">The original entity text.</param>
    /// <param name="window">Words surrounding the entity.</param>
    /// <param name="confidence">Confidence score of the detection.</param>
    /// <param name="classification">Optional entity classification label.</param>
    /// <param name="filterPattern">The pattern that produced the match.</param>
    /// <param name="crypto">AES crypto settings, or <see langword="null"/>.</param>
    /// <param name="fpe">Format-preserving encryption settings, or <see langword="null"/>.</param>
    /// <param name="filterType">The type of filter invoking this method (used for default redaction format).</param>
    /// <returns>A <see cref="Replacement"/> containing the replacement value and optional salt.</returns>
    protected Replacement GetStandardReplacement(string context, string token, string[] window, double confidence, string? classification, FilterPattern? filterPattern, Crypto? crypto, Fpe? fpe, FilterType filterType)
    {
        var salt = Salt ? GenerateSalt() : string.Empty;

        return Strategy switch
        {
            Redact => new Replacement(GetRedactedToken(token, classification, filterType), salt, true),
            RandomReplace => new Replacement(GetOrCreateRandomReplacement(context, token), salt, true),
            StaticReplace => new Replacement(!string.IsNullOrEmpty(StaticReplacement) ? StaticReplacement : GetRedactedToken(token, classification, filterType), salt, true),
            Mask => new Replacement(MaskToken(token), salt, true),
            Last4 => new Replacement(token.Length >= 4 ? token[^4..] : token, salt, true),
            HashSha256Replace => new Replacement(HashSha256(token + salt), salt, true),
            CryptoReplace => crypto != null ? new Replacement(AesEncrypt(token, crypto), salt, true) : new Replacement(GetRedactedToken(token, classification, filterType), salt, true),
            Same => new Replacement(token, salt, false),
            Truncate => new Replacement(token.Length > 0 ? token[..1] : token, salt, true),
            _ => new Replacement(GetRedactedToken(token, classification, filterType), salt, true)
        };
    }

    private string GetOrCreateRandomReplacement(string context, string token)
    {
        if (ContextService != null)
        {
            var existing = ContextService.Get(context, token);
            if (existing != null) return existing;
            var random = Guid.NewGuid().ToString();
            ContextService.Put(context, token, random);
            return random;
        }
        return Guid.NewGuid().ToString();
    }

    private string MaskToken(string token)
    {
        if (MaskLength == "same") return new string(MaskCharacter[0], token.Length);
        if (int.TryParse(MaskLength, out int len)) return new string(MaskCharacter[0], Math.Min(len, token.Length));
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

    private static string AesEncrypt(string plaintext, Crypto crypto)
    {
        try
        {
            var key = Convert.FromBase64String(crypto.Key ?? string.Empty);
            var iv = Convert.FromBase64String(crypto.Iv ?? string.Empty);
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            var encryptor = aes.CreateEncryptor();
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var encrypted = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);
            return Convert.ToBase64String(encrypted);
        }
        catch
        {
            return plaintext;
        }
    }
}
