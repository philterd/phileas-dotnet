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
            FpeEncryptReplace => fpe != null ? new Replacement(FpeEncrypt(token, fpe), salt, true) : new Replacement(GetRedactedToken(token, classification, filterType), salt, true),
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

    private static string FpeEncrypt(string token, Fpe fpe)
    {
        if (string.IsNullOrEmpty(fpe.Key)) return token;
        try
        {
            var key = Convert.FromBase64String(fpe.Key);
            var tweak = string.IsNullOrEmpty(fpe.Tweak) ? [] : Convert.FromBase64String(fpe.Tweak);
            var chars = token.ToCharArray();
            FpeEncryptClass(chars, key, tweak, char.IsDigit, 10, '0');
            FpeEncryptClass(chars, key, tweak, char.IsLower, 26, 'a');
            FpeEncryptClass(chars, key, tweak, char.IsUpper, 26, 'A');
            return new string(chars);
        }
        catch
        {
            return token;
        }
    }

    private static void FpeEncryptClass(char[] chars, byte[] key, byte[] tweak, Func<char, bool> pred, int radix, char baseChar)
    {
        int i = 0;
        while (i < chars.Length)
        {
            if (!pred(chars[i])) { i++; continue; }
            int start = i;
            while (i < chars.Length && pred(chars[i])) i++;
            if (i - start >= 1)
                FpeEncryptSegment(chars, start, i - start, key, tweak, radix, baseChar);
        }
    }

    private static void FpeEncryptSegment(char[] chars, int start, int len, byte[] key, byte[] tweak, int radix, char baseChar)
    {
        var vals = new int[len];
        for (int i = 0; i < len; i++) vals[i] = chars[start + i] - baseChar;

        int u = (len + 1) / 2;
        var A = vals[..u].ToArray();
        var B = vals[u..].ToArray();

        for (int round = 0; round < 8; round++)
        {
            if (round % 2 == 0)
            {
                var prf = FpeRoundPrf(key, tweak, round, radix, start, B);
                for (int i = 0; i < A.Length; i++)
                    A[i] = (A[i] + prf[i % prf.Length]) % radix;
            }
            else
            {
                var prf = FpeRoundPrf(key, tweak, round, radix, start, A);
                for (int i = 0; i < B.Length; i++)
                    B[i] = (B[i] + prf[i % prf.Length]) % radix;
            }
            (A, B) = (B, A);
        }

        Array.Copy(A, 0, vals, 0, A.Length);
        Array.Copy(B, 0, vals, A.Length, B.Length);
        for (int i = 0; i < len; i++) chars[start + i] = (char)(baseChar + vals[i]);
    }

    private static int[] FpeRoundPrf(byte[] key, byte[] tweak, int round, int radix, int startIndex, int[] input)
    {
        int sz = tweak.Length + 8 + input.Length * 2;
        var data = new byte[sz];
        int pos = 0;
        Array.Copy(tweak, 0, data, pos, tweak.Length); pos += tweak.Length;
        data[pos++] = (byte)round;
        data[pos++] = (byte)(radix >> 8); data[pos++] = (byte)radix;
        data[pos++] = (byte)(startIndex >> 24); data[pos++] = (byte)(startIndex >> 16);
        data[pos++] = (byte)(startIndex >> 8); data[pos++] = (byte)startIndex;
        data[pos++] = 0;
        foreach (var v in input) { data[pos++] = (byte)(v >> 8); data[pos++] = (byte)v; }
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(data);
        return hash.Select(b => b % radix).ToArray();
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
