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

namespace Phileas.Utils;

/// <summary>
///     FF3-1 format-preserving encryption (NIST SP 800-38G Rev. 1). A faithful port of the mysto
///     <c>com.privacylogistics.FF3Cipher</c> reference used by the Java implementation, so it produces
///     the same ciphertext for the same key, tweak and input.
/// </summary>
public class FF3Cipher
{
    private const int NumRounds = 8;
    private const int BlockSize = 16;
    private const int TweakLen = 8; // original FF3 64-bit tweak
    private const int TweakLenNew = 7; // FF3-1 56-bit tweak
    private const int HalfTweakLen = TweakLen / 2;
    private const int MaxRadix = 256;

    /// <summary>The minimum domain size required by FF3-1 (one million).</summary>
    public const int DomainMin = 1000000;

    private const string Digits = "0123456789";
    private const string AsciiLowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string AsciiUppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private readonly int _radix;
    private readonly string _alphabet;
    private readonly byte[] _defaultTweak;
    private readonly byte[] _aesKey;
    private readonly int _minLen;
    private readonly int _maxLen;

    /// <summary>Creates a base-10 FF3 cipher from hex-encoded key and tweak.</summary>
    public FF3Cipher(string key, string tweak) : this(key, tweak, 10)
    {
    }

    /// <summary>Creates an FF3 cipher for the given radix from hex-encoded key and tweak.</summary>
    public FF3Cipher(string key, string tweak, int radix)
        : this(Convert.FromHexString(key), Convert.FromHexString(tweak), AlphabetForBase(radix))
    {
    }

    /// <summary>Creates an FF3 cipher over a custom alphabet.</summary>
    public FF3Cipher(byte[] key, byte[] tweak, string alphabet)
    {
        _alphabet = alphabet;
        _radix = alphabet.Length;
        _minLen = (int)Math.Ceiling(Math.Log(DomainMin) / Math.Log(_radix));
        _maxLen = (int)(2 * Math.Floor(Math.Log(Math.Pow(2, 96)) / Math.Log(_radix)));

        if (key.Length != 16 && key.Length != 24 && key.Length != 32)
            throw new ArgumentException($"key length {key.Length} but must be 128, 192, or 256 bits");
        if (_radix is < 2 or > MaxRadix)
            throw new ArgumentException("radix must be between 2 and 256, inclusive");
        if (_minLen < 2 || _maxLen < _minLen)
            throw new ArgumentException("minLen or maxLen invalid, adjust your radix");

        _defaultTweak = tweak;

        // FF3 keys the AES core with the byte-reversed key.
        var reversedKey = (byte[])key.Clone();
        ReverseBytes(reversedKey);
        _aesKey = reversedKey;
    }

    /// <summary>The minimum supported message length.</summary>
    public int MinMessageLength => _minLen;

    /// <summary>The maximum supported message length.</summary>
    public int MaxMessageLength => _maxLen;

    /// <summary>Encrypts <paramref name="plaintext" /> with the default tweak.</summary>
    public string Encrypt(string plaintext) => Encrypt(plaintext, _defaultTweak);

    /// <summary>Encrypts <paramref name="plaintext" /> with the given tweak.</summary>
    public string Encrypt(string plaintext, byte[] tweak)
    {
        var n = plaintext.Length;
        if (n < _minLen || n > _maxLen)
            throw new ArgumentException($"message length {n} is not within min {_minLen} and max {_maxLen} bounds");

        var u = (int)Math.Ceiling(n / 2.0);
        var v = n - u;
        var a = plaintext[..u].ToCharArray();
        var b = plaintext[u..].ToCharArray();

        var (tl, tr) = SplitTweak(tweak);
        var modU = BigInteger.Pow(_radix, u);
        var modV = BigInteger.Pow(_radix, v);

        for (var i = 0; i < NumRounds; i++)
        {
            var (m, w) = i % 2 == 0 ? (u, tr) : (v, tl);

            var p = CalculateP(i, _alphabet, w, b);
            ReverseBytes(p);
            var s = EncryptAesBlock(p);
            ReverseBytes(s);

            var y = new BigInteger(s, isUnsigned: true, isBigEndian: true);
            var c = DecodeIntR(a, _alphabet) + y;
            c = Mod(c, i % 2 == 0 ? modU : modV);

            var encoded = EncodeIntR(c, _alphabet, m);
            a = b;
            b = encoded;
        }

        return new string(a) + new string(b);
    }

    /// <summary>Decrypts <paramref name="ciphertext" /> with the default tweak.</summary>
    public string Decrypt(string ciphertext) => Decrypt(ciphertext, _defaultTweak);

    /// <summary>Decrypts <paramref name="ciphertext" /> with the given tweak.</summary>
    public string Decrypt(string ciphertext, byte[] tweak)
    {
        var n = ciphertext.Length;
        if (n < _minLen || n > _maxLen)
            throw new ArgumentException($"message length {n} is not within min {_minLen} and max {_maxLen} bounds");

        var u = (int)Math.Ceiling(n / 2.0);
        var v = n - u;
        var a = ciphertext[..u].ToCharArray();
        var b = ciphertext[u..].ToCharArray();

        var (tl, tr) = SplitTweak(tweak);
        var modU = BigInteger.Pow(_radix, u);
        var modV = BigInteger.Pow(_radix, v);

        for (var i = NumRounds - 1; i >= 0; i--)
        {
            var (m, w) = i % 2 == 0 ? (u, tr) : (v, tl);

            var p = CalculateP(i, _alphabet, w, a);
            ReverseBytes(p);
            var s = EncryptAesBlock(p);
            ReverseBytes(s);

            var y = new BigInteger(s, isUnsigned: true, isBigEndian: true);
            var c = DecodeIntR(b, _alphabet) - y;
            c = Mod(c, i % 2 == 0 ? modU : modV);

            var encoded = EncodeIntR(c, _alphabet, m);
            b = a;
            a = encoded;
        }

        return new string(a) + new string(b);
    }

    private (byte[] Tl, byte[] Tr) SplitTweak(byte[] tweak)
    {
        if (tweak.Length != TweakLen && tweak.Length != TweakLenNew)
            throw new ArgumentException($"tweak length {tweak.Length} is invalid: tweak must be 56 or 64 bits");

        var tweak64 = tweak.Length == TweakLenNew ? CalculateTweak64(tweak) : tweak;
        return (tweak64[..HalfTweakLen], tweak64[HalfTweakLen..TweakLen]);
    }

    private byte[] EncryptAesBlock(byte[] block)
    {
        using var aes = Aes.Create();
        aes.Key = _aesKey;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        return aes.EncryptEcb(block, PaddingMode.None);
    }

    private static byte[] CalculateP(int i, string alphabet, byte[] w, char[] b)
    {
        var p = new byte[BlockSize];
        p[0] = w[0];
        p[1] = w[1];
        p[2] = w[2];
        p[3] = (byte)(w[3] ^ i);

        var val = DecodeIntR(b, alphabet);
        var bBytes = val.IsZero
            ? new byte[] { 0 }
            : val.ToByteArray(isUnsigned: true, isBigEndian: true);

        // Right-align the value's big-endian magnitude into the trailing 12 bytes of P.
        var copyLen = Math.Min(bBytes.Length, 12);
        Array.Copy(bBytes, bBytes.Length - copyLen, p, BlockSize - copyLen, copyLen);
        return p;
    }

    private static char[] EncodeIntR(BigInteger n, string alphabet, int length)
    {
        var x = new char[length];
        var i = 0;
        var bbase = (BigInteger)alphabet.Length;

        while (n >= bbase)
        {
            var b = n % bbase;
            n /= bbase;
            x[i++] = alphabet[(int)b];
        }

        x[i++] = alphabet[(int)n];

        while (i < length)
        {
            x[i++] = alphabet[0];
        }

        return x;
    }

    private static BigInteger DecodeIntR(char[] str, string alphabet)
    {
        var bas = (BigInteger)alphabet.Length;
        var num = BigInteger.Zero;
        for (var i = 0; i < str.Length; i++)
        {
            num += BigInteger.Pow(bas, i) * alphabet.IndexOf(str[i]);
        }

        return num;
    }

    private static byte[] CalculateTweak64(byte[] t)
    {
        return new[]
        {
            t[0], t[1], t[2], (byte)(t[3] & 0xF0), t[4], t[5], t[6], (byte)((t[3] & 0x0F) << 4)
        };
    }

    private static void ReverseBytes(byte[] b)
    {
        for (var i = 0; i < b.Length / 2; i++)
        {
            (b[i], b[b.Length - i - 1]) = (b[b.Length - i - 1], b[i]);
        }
    }

    private static BigInteger Mod(BigInteger value, BigInteger modulus)
    {
        var result = value % modulus;
        return result.Sign < 0 ? result + modulus : result;
    }

    private static string AlphabetForBase(int radix)
    {
        return radix switch
        {
            10 => Digits,
            36 => Digits + AsciiUppercase,
            62 => Digits + AsciiUppercase + AsciiLowercase,
            _ => throw new ArgumentException("Unsupported radix")
        };
    }
}
