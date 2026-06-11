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

namespace Phileas.Data.Generators;

/// <summary>Generates random Bitcoin-style addresses beginning with "1".</summary>
public class BitcoinAddressGenerator : IGenerator<string>
{
    private const string Chars = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private readonly Random _random;

    public BitcoinAddressGenerator(Random random) => _random = random;

    public string Random()
    {
        var sb = new System.Text.StringBuilder("1");
        for (var i = 0; i < 33; i++)
        {
            sb.Append(Chars[_random.Next(Chars.Length)]);
        }
        return sb.ToString();
    }

    public long PoolSize() => long.MaxValue;
}
