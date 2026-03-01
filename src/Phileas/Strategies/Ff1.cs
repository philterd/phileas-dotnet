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

using System.Numerics;
using System.Security.Cryptography;

namespace Phileas.Strategies;

/// <summary>
/// Implements the FF1 Format-Preserving Encryption algorithm as specified in NIST SP 800-38G Rev 1.
/// Alphanumeric characters are encrypted within their own character class (digits→digits,
/// uppercase→uppercase, lowercase→lowercase); non-alphanumeric characters are preserved in place.
/// </summary>
internal static class Ff1
{
    private const int NumRounds = 10;
    private const int BlockSize = 16;

    private const string Digits = "0123456789";
    private const string UpperLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LowerLetters = "abcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// Encrypts a string using FF1, preserving the format of the input.
    /// Digits, uppercase letters, and lowercase letters are each encrypted within their own class.
    /// Non-alphanumeric characters are left unchanged.
    /// </summary>
    /// <param name="plaintext">The input string to encrypt.</param>
    /// <param name="key">The AES key (16, 24, or 32 bytes).</param>
    /// <param name="tweak">The FF1 tweak value (may be empty).</param>
    /// <returns>The format-preserved encrypted string.</returns>
    public static string Encrypt(string plaintext, byte[] key, byte[] tweak)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;

        var result = plaintext.ToCharArray();

        // Process each character class independently so the output preserves the type of each character.
        result = EncryptClass(result, Digits, key, tweak);
        result = EncryptClass(result, UpperLetters, key, DeriveClassTweak(tweak, 1));
        result = EncryptClass(result, LowerLetters, key, DeriveClassTweak(tweak, 2));

        return new string(result);
    }

    /// <summary>
    /// Applies FF1 to all characters that belong to <paramref name="alphabet"/> and returns the updated char array.
    /// </summary>
    private static char[] EncryptClass(char[] chars, string alphabet, byte[] key, byte[] tweak)
    {
        // Collect positions of characters belonging to this alphabet.
        var positions = new List<int>();
        for (int i = 0; i < chars.Length; i++)
        {
            if (alphabet.Contains(chars[i]))
                positions.Add(i);
        }

        if (positions.Count < 2)
            return chars; // FF1 requires n ≥ 2; skip if too few characters.

        int radix = alphabet.Length;

        // Map input characters to alphabet indices.
        var X = positions.Select(p => alphabet.IndexOf(chars[p])).ToArray();

        // Run FF1.
        var encrypted = EncryptCore(X, radix, key, tweak);

        // Write encrypted characters back.
        for (int i = 0; i < positions.Count; i++)
            chars[positions[i]] = alphabet[encrypted[i]];

        return chars;
    }

    /// <summary>
    /// Creates a class-specific tweak by appending a one-byte discriminator to the base tweak,
    /// ensuring digits, uppercase, and lowercase are encrypted independently.
    /// </summary>
    private static byte[] DeriveClassTweak(byte[] tweak, byte discriminator)
    {
        var derived = new byte[tweak.Length + 1];
        Buffer.BlockCopy(tweak, 0, derived, 0, tweak.Length);
        derived[tweak.Length] = discriminator;
        return derived;
    }

    /// <summary>
    /// Core FF1 Feistel network (NIST SP 800-38G Rev 1, Algorithm 7).
    /// </summary>
    private static int[] EncryptCore(int[] X, int radix, byte[] key, byte[] tweak)
    {
        int n = X.Length;
        int u = (n + 1) / 2; // ceil(n/2)
        int v = n - u;

        // b = ceil(ceil(v * log2(radix)) / 8)
        int b = (int)Math.Ceiling(Math.Ceiling(v * Math.Log2(radix)) / 8);
        if (b < 1) b = 1;

        // d = 4 * ceil(b/4) + 4
        int d = 4 * ((b + 3) / 4) + 4;

        var A = X[..u];
        var B = X[u..];

        // Build the fixed P block (16 bytes).
        var P = new byte[BlockSize];
        P[0] = 1; P[1] = 2; P[2] = 1;
        P[3] = (byte)((radix >> 16) & 0xFF);
        P[4] = (byte)((radix >> 8) & 0xFF);
        P[5] = (byte)(radix & 0xFF);
        P[6] = NumRounds;
        P[7] = (byte)(u & 0xFF);
        P[8] = (byte)((n >> 24) & 0xFF);
        P[9] = (byte)((n >> 16) & 0xFF);
        P[10] = (byte)((n >> 8) & 0xFF);
        P[11] = (byte)(n & 0xFF);
        P[12] = (byte)((tweak.Length >> 24) & 0xFF);
        P[13] = (byte)((tweak.Length >> 16) & 0xFF);
        P[14] = (byte)((tweak.Length >> 8) & 0xFF);
        P[15] = (byte)(tweak.Length & 0xFF);

        using var aes = Aes.Create();
        aes.Key = key;

        for (int i = 0; i < NumRounds; i++)
        {
            int m = (i % 2 == 0) ? u : v;

            // Q = T || [0]^((-|T|-b-1) mod 16) || [i]^1 || NUMradix(B) as b bytes.
            var numBBytes = BigIntToBytes(NumRadix(B, radix), b);
            int padLen = (-(tweak.Length + b + 1) % BlockSize + BlockSize) % BlockSize;
            var Q = new byte[tweak.Length + padLen + 1 + b];
            Buffer.BlockCopy(tweak, 0, Q, 0, tweak.Length);
            Q[tweak.Length + padLen] = (byte)i;
            Buffer.BlockCopy(numBBytes, 0, Q, tweak.Length + padLen + 1, b);

            // R = PRF(K, P || Q).
            var PQ = new byte[P.Length + Q.Length];
            Buffer.BlockCopy(P, 0, PQ, 0, P.Length);
            Buffer.BlockCopy(Q, 0, PQ, P.Length, Q.Length);
            var R = Prf(aes, PQ);

            // S = R || REVB(CIPH_K(REVB(R) XOR [j])) for j = 1, 2, … until |S| >= d.
            int extraBlocks = (d - 1) / BlockSize;
            var S = new byte[BlockSize * (1 + extraBlocks)];
            Buffer.BlockCopy(R, 0, S, 0, BlockSize);

            var revR = Reverse(R);
            for (int j = 1; j <= extraBlocks; j++)
            {
                var xorBlock = (byte[])revR.Clone();
                XorWithInt(xorBlock, j);
                var encBlock = EncryptBlock(aes, xorBlock);
                Buffer.BlockCopy(Reverse(encBlock), 0, S, j * BlockSize, BlockSize);
            }

            // y = NUM(S[0..d-1]).
            var y = BytesToBigInt(S[..d]);

            // c = (NUMradix(REVB(A)) + y) mod radix^m.
            var radixPowM = BigIntPow(radix, m);
            var c = (NumRadix(Reverse(A), radix) + y) % radixPowM;

            // C = REVB(STR^m_radix(c)).
            var C = Reverse(StrRadix(c, radix, m));

            A = B;
            B = C;
        }

        var result = new int[n];
        A.CopyTo(result, 0);
        B.CopyTo(result, u);
        return result;
    }

    /// <summary>
    /// Computes PRF(K, data) = last 16-byte block of AES-CBC-MAC over <paramref name="data"/>.
    /// <paramref name="data"/> must be a multiple of 16 bytes.
    /// </summary>
    private static byte[] Prf(Aes aes, byte[] data)
    {
        // AES-CBC with zero IV is equivalent to CBC-MAC; the last block is the PRF output.
        var iv = new byte[BlockSize];
        var ciphertext = aes.EncryptCbc(data, iv, PaddingMode.None);
        return ciphertext[(ciphertext.Length - BlockSize)..];
    }

    private static byte[] EncryptBlock(Aes aes, byte[] block)
    {
        return aes.EncryptEcb(block, PaddingMode.None);
    }

    private static BigInteger NumRadix(int[] X, int radix)
    {
        BigInteger result = 0;
        foreach (int x in X)
            result = result * radix + x;
        return result;
    }

    private static int[] StrRadix(BigInteger x, int radix, int m)
    {
        var result = new int[m];
        for (int i = m - 1; i >= 0; i--)
        {
            result[i] = (int)(x % radix);
            x /= radix;
        }
        return result;
    }

    private static byte[] BigIntToBytes(BigInteger n, int length)
    {
        var bytes = n.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (bytes.Length == length) return bytes;
        if (bytes.Length > length) return bytes[(bytes.Length - length)..];
        var padded = new byte[length];
        Buffer.BlockCopy(bytes, 0, padded, length - bytes.Length, bytes.Length);
        return padded;
    }

    private static BigInteger BytesToBigInt(byte[] bytes)
        => new BigInteger(bytes, isUnsigned: true, isBigEndian: true);

    private static BigInteger BigIntPow(int radix, int exp)
    {
        BigInteger result = 1;
        var r = new BigInteger(radix);
        for (int i = 0; i < exp; i++)
            result *= r;
        return result;
    }

    private static T[] Reverse<T>(T[] arr)
    {
        var copy = (T[])arr.Clone();
        Array.Reverse(copy);
        return copy;
    }

    /// <summary>XORs the last 4 bytes of <paramref name="block"/> with the big-endian representation of <paramref name="value"/>.</summary>
    private static void XorWithInt(byte[] block, int value)
    {
        block[15] ^= (byte)(value & 0xFF);
        block[14] ^= (byte)((value >> 8) & 0xFF);
        block[13] ^= (byte)((value >> 16) & 0xFF);
        block[12] ^= (byte)((value >> 24) & 0xFF);
    }
}
