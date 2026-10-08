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

/// <summary>
///     Generates random, Luhn-valid Canadian Social Insurance Numbers in <c>NNN-NNN-NNN</c> format. The first digit
///     is never 0 or 8, neither of which is issued as a personal SIN.
/// </summary>
public class CanadaSinGenerator : IGenerator<string>
{
    private static readonly int[] FirstDigits = { 1, 2, 3, 4, 5, 6, 7, 9 };

    private readonly Random _random;

    /// <summary>Creates a new <see cref="CanadaSinGenerator" />.</summary>
    public CanadaSinGenerator(Random random)
    {
        _random = random;
    }

    /// <inheritdoc />
    public string Random()
    {
        var digits = new int[9];
        digits[0] = FirstDigits[_random.Next(FirstDigits.Length)];
        for (var i = 1; i < 8; i++) digits[i] = _random.Next(10);

        // The check digit is the ninth, so counting from it the doubled digits are the second, fourth, sixth and
        // eighth (indexes 1, 3, 5 and 7).
        var sum = 0;
        for (var i = 0; i < 8; i++)
        {
            var digit = digits[i];
            if (i % 2 == 1)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
        }
        digits[8] = (10 - sum % 10) % 10;

        var s = string.Concat(digits);
        return $"{s[..3]}-{s[3..6]}-{s[6..]}";
    }

    /// <inheritdoc />
    public long PoolSize()
    {
        return FirstDigits.Length * 10_000_000L;
    }
}
