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

namespace Phileas.Utils;

/// <summary>
///     MurmurHash3 x86 32-bit hash, ported to match Apache commons-codec's
///     <c>MurmurHash3.hash32x86(byte[])</c> (seed 0) so token hashing is deterministic and stable across
///     runs. Used by span disambiguation to map context tokens to vector indexes.
/// </summary>
public static class MurmurHash3
{
    private const uint C1 = 0xcc9e2d51;
    private const uint C2 = 0x1b873593;
    private const int R1 = 15;
    private const int R2 = 13;
    private const uint M = 5;
    private const uint N = 0xe6546b64;

    /// <summary>Computes the 32-bit MurmurHash3 (x86 variant) of <paramref name="data" /> with seed 0.</summary>
    public static int Hash32X86(byte[] data)
    {
        return Hash32X86(data, 0, data.Length, 0);
    }

    /// <summary>Computes the 32-bit MurmurHash3 (x86 variant) over a region of <paramref name="data" />.</summary>
    public static int Hash32X86(byte[] data, int offset, int length, int seed)
    {
        unchecked
        {
            var hash = (uint)seed;
            var nblocks = length >> 2;

            // body
            for (var i = 0; i < nblocks; i++)
            {
                var index = offset + (i << 2);
                var k = (uint)(data[index]
                               | (data[index + 1] << 8)
                               | (data[index + 2] << 16)
                               | (data[index + 3] << 24));
                hash = Mix32(k, hash);
            }

            // tail
            var tailIndex = offset + (nblocks << 2);
            uint k1 = 0;
            switch (offset + length - tailIndex)
            {
                case 3:
                    k1 ^= (uint)(data[tailIndex + 2] & 0xff) << 16;
                    goto case 2;
                case 2:
                    k1 ^= (uint)(data[tailIndex + 1] & 0xff) << 8;
                    goto case 1;
                case 1:
                    k1 ^= (uint)(data[tailIndex] & 0xff);
                    k1 *= C1;
                    k1 = RotateLeft(k1, R1);
                    k1 *= C2;
                    hash ^= k1;
                    break;
            }

            hash ^= (uint)length;
            return (int)Fmix32(hash);
        }
    }

    private static uint Mix32(uint k, uint hash)
    {
        unchecked
        {
            k *= C1;
            k = RotateLeft(k, R1);
            k *= C2;
            hash ^= k;
            return RotateLeft(hash, R2) * M + N;
        }
    }

    private static uint Fmix32(uint hash)
    {
        unchecked
        {
            hash ^= hash >> 16;
            hash *= 0x85ebca6b;
            hash ^= hash >> 13;
            hash *= 0xc2b2ae35;
            hash ^= hash >> 16;
            return hash;
        }
    }

    private static uint RotateLeft(uint value, int bits)
    {
        return (value << bits) | (value >> (32 - bits));
    }
}
