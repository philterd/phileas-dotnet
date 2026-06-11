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

/// <summary>Generates random Vehicle Identification Numbers, optionally with a valid check digit.</summary>
public class VinGenerator : IGenerator<string>
{
    private const string Chars = "0123456789ABCDEFGHJKLMNPRSTUVWXYZ";
    private readonly Random _random;
    private readonly bool _onlyValid;

    public VinGenerator(Random random) : this(random, false) { }

    public VinGenerator(Random random, bool onlyValid)
    {
        _random = random;
        _onlyValid = onlyValid;
    }

    public string Random()
    {
        if (_onlyValid) return GenerateValidVin();

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < 17; i++) sb.Append(Chars[_random.Next(Chars.Length)]);
        return sb.ToString();
    }

    private string GenerateValidVin()
    {
        int[] values = { 1, 2, 3, 4, 5, 6, 7, 8, 0, 1, 2, 3, 4, 5, 0, 7, 0, 9, 2, 3, 4, 5, 6, 7, 8, 9 };
        int[] weights = { 8, 7, 6, 5, 4, 3, 2, 10, 0, 9, 8, 7, 6, 5, 4, 3, 2 };

        var sb = new System.Text.StringBuilder();
        var sum = 0;
        for (var i = 0; i < 17; i++)
        {
            char c;
            if (i == 8)
            {
                c = '0'; // check digit placeholder
            }
            else
            {
                c = Chars[_random.Next(Chars.Length)];
            }
            sb.Append(c);
            var value = c >= 'A' && c <= 'Z' ? values[c - 'A'] : c - '0';
            sum += value * weights[i];
        }

        var checkDigitValue = sum % 11;
        var checkDigit = checkDigitValue == 10 ? 'X' : (char)(checkDigitValue + '0');
        sb[8] = checkDigit;
        return sb.ToString();
    }

    public long PoolSize() => long.MaxValue;
}
