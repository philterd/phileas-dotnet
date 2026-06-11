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
using Phileas.Policy;
using Phileas.Utils;
using Xunit;

namespace Phileas.Tests;

public class EncryptionTests
{
    private const string Key = "9EE7A356FDFE43F069500B0086758346E66D8583E0CE1CFCA04E50F67ECCE5D1";

    [Theory]
    [InlineData("D8E7920AFA330A73", "890121234567890000", "750918814058654607")]
    [InlineData("9A768A92F60E12D8", "890121234567890000", "018989839189395384")]
    [InlineData("D8E7920AFA330A73", "89012123456789000000789000000", "48598367162252569629397416226")]
    public void FormatPreservingEncryptionMatchesFf3Vectors(string tweak, string plainText, string expected)
    {
        var fpe = new Fpe("EF4359D8D580AA4F7F036D6F04FC6A94", tweak);

        var encrypted = Encryption.FormatPreservingEncrypt(fpe, plainText);

        Assert.Equal(expected, encrypted);
        Assert.Equal(plainText, Encryption.FormatPreservingDecrypt(fpe, encrypted));
    }

    [Fact]
    public void FormatPreservingEncryptionRejectsShortInput()
    {
        var fpe = new Fpe("EF4359D8D580AA4F7F036D6F04FC6A94", "D8E7920AFA330A73");
        Assert.Throws<FormatPreservingEncryptionException>(() => Encryption.FormatPreservingEncrypt(fpe, "12345"));
    }

    [Fact]
    public void FormatPreservingEncryptionRejectsLongInput()
    {
        var fpe = new Fpe("EF4359D8D580AA4F7F036D6F04FC6A94", "D8E7920AFA330A73");
        var tooLong = new string('1', 57);
        Assert.Throws<FormatPreservingEncryptionException>(() => Encryption.FormatPreservingEncrypt(fpe, tooLong));
    }

    [Fact]
    public void EncryptDecryptRoundTrips()
    {
        var crypto = new Crypto(Key, null);
        const string token = "346596542547526";

        var encrypted = Encryption.Encrypt(token, crypto);

        Assert.Equal(token, Encryption.Decrypt(encrypted, crypto));
    }

    [Fact]
    public void EncryptionIsNonDeterministic()
    {
        // A fresh random nonce per call means the same plaintext encrypts to different ciphertext, so
        // identical values do not produce identical redactions across the corpus.
        var crypto = new Crypto(Key, null);
        const string token = "346596542547526";

        Assert.NotEqual(Encryption.Encrypt(token, crypto), Encryption.Encrypt(token, crypto));
    }

    [Fact]
    public void DecryptRejectsTamperedCiphertext()
    {
        var crypto = new Crypto(Key, null);
        var encrypted = Encryption.Encrypt("sensitive-value", crypto);

        // Flip the last byte (part of the GCM authentication tag); decryption must fail.
        var raw = Convert.FromBase64String(encrypted);
        raw[^1] ^= 0x01;
        var tampered = Convert.ToBase64String(raw);

        Assert.ThrowsAny<CryptographicException>(() => Encryption.Decrypt(tampered, crypto));
    }
}
