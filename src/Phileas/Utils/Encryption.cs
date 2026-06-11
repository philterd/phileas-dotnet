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
using Phileas.Policy;

namespace Phileas.Utils;

/// <summary>
///     Authenticated encryption used by the <c>CRYPTO_REPLACE</c> strategy. Mirrors the Java reference:
///     AES-GCM with a fresh random nonce per value and a 128-bit authentication tag. The encryption key
///     is a hexadecimal-encoded AES key (128-, 192-, or 256-bit). The nonce is prepended to the
///     ciphertext so each encrypted value is self-contained: <c>nonce || ciphertext || tag</c>, Base64-encoded.
/// </summary>
public static class Encryption
{
    private const int GcmNonceLength = 12; // 96-bit nonce, recommended for GCM
    private const int GcmTagLength = 16; // 128-bit authentication tag

    // FF3 supports a bounded input length: the domain must be large enough to be secure and within FF3's
    // block limits. Values whose format-preservable content falls outside this range cannot be encrypted.
    private const int FpeMinLength = 6;
    private const int FpeMaxLength = 56;

    /// <summary>
    ///     Format-preserving encryption of <paramref name="token" /> via FF3-1: only the digit/letter
    ///     characters are encrypted (as a single string), with all other characters left in place.
    ///     The <paramref name="fpe" /> key and tweak are hex-encoded. Mirrors the Java reference.
    /// </summary>
    public static string FormatPreservingEncrypt(Fpe fpe, string token)
        => ReassembleAroundStructure(token, encryptable => DoFormatPreserving(encryptable, fpe, encrypt: true));

    /// <summary>Reverses <see cref="FormatPreservingEncrypt" />.</summary>
    public static string FormatPreservingDecrypt(Fpe fpe, string token)
        => ReassembleAroundStructure(token, encryptable => DoFormatPreserving(encryptable, fpe, encrypt: false));

    private static string ReassembleAroundStructure(string token, Func<string, string> transform)
    {
        var structural = new char[token.Length];
        var encryptable = new StringBuilder();

        for (var i = 0; i < token.Length; i++)
        {
            var c = token[i];
            if (!char.IsDigit(c) && !char.IsLetter(c))
            {
                structural[i] = c;
            }
            else
            {
                encryptable.Append(c);
            }
        }

        var transformed = transform(encryptable.ToString());

        var transformedIndex = 0;
        for (var i = 0; i < structural.Length; i++)
        {
            if (structural[i] == '\0')
            {
                structural[i] = transformed[transformedIndex++];
            }
        }

        return new string(structural);
    }

    private static string DoFormatPreserving(string content, Fpe fpe, bool encrypt)
    {
        if (content.Length < FpeMinLength || content.Length > FpeMaxLength)
        {
            throw new FormatPreservingEncryptionException(
                $"The value's format-preservable content ({content.Length} characters) is outside the supported "
                + $"range of {FpeMinLength} to {FpeMaxLength} characters.");
        }

        try
        {
            var cipher = new FF3Cipher(fpe.GetKey() ?? string.Empty, fpe.GetTweak() ?? string.Empty);
            return encrypt ? cipher.Encrypt(content) : cipher.Decrypt(content);
        }
        catch (Exception ex)
        {
            // The plain text is intentionally not included in the message.
            throw new FormatPreservingEncryptionException("The value could not be format-preserving encrypted.", ex);
        }
    }

    /// <summary>
    ///     Encrypts <paramref name="token" /> with AES-GCM using the (hex-encoded) key from
    ///     <paramref name="crypto" />. Returns Base64 of <c>nonce || ciphertext || tag</c>.
    /// </summary>
    public static string Encrypt(string token, Crypto crypto)
    {
        var key = Convert.FromHexString(crypto.GetKey() ?? string.Empty);
        var nonce = RandomNumberGenerator.GetBytes(GcmNonceLength);
        var plaintext = Encoding.UTF8.GetBytes(token);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[GcmTagLength];

        using (var aes = new AesGcm(key, GcmTagLength))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        // Lay out nonce || ciphertext || tag so the value is self-contained and matches the Java
        // reference (whose GCM output is ciphertext || tag).
        var output = new byte[nonce.Length + ciphertext.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, nonce.Length);
        Buffer.BlockCopy(ciphertext, 0, output, nonce.Length, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, output, nonce.Length + ciphertext.Length, tag.Length);
        return Convert.ToBase64String(output);
    }

    /// <summary>
    ///     Decrypts a value produced by <see cref="Encrypt" />, validating the authentication tag.
    /// </summary>
    public static string Decrypt(string encrypted, Crypto crypto)
    {
        var key = Convert.FromHexString(crypto.GetKey() ?? string.Empty);
        var input = Convert.FromBase64String(encrypted);
        if (input.Length < GcmNonceLength + GcmTagLength)
        {
            throw new ArgumentException("The encrypted value is too short to contain a nonce and tag.");
        }

        var nonce = new byte[GcmNonceLength];
        var tag = new byte[GcmTagLength];
        var ciphertext = new byte[input.Length - GcmNonceLength - GcmTagLength];
        Buffer.BlockCopy(input, 0, nonce, 0, GcmNonceLength);
        Buffer.BlockCopy(input, GcmNonceLength, ciphertext, 0, ciphertext.Length);
        Buffer.BlockCopy(input, GcmNonceLength + ciphertext.Length, tag, 0, GcmTagLength);

        var plaintext = new byte[ciphertext.Length];
        using (var aes = new AesGcm(key, GcmTagLength))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        return Encoding.UTF8.GetString(plaintext);
    }
}
