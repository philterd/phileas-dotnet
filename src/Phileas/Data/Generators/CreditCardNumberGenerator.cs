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

/// <summary>Generates random credit-card numbers, optionally Luhn-valid.</summary>
public class CreditCardNumberGenerator : IGenerator<string>
{
    private readonly Random _random;
    private readonly bool _valid;

    public CreditCardNumberGenerator(Random random) : this(random, false) { }

    public CreditCardNumberGenerator(Random random, bool valid)
    {
        _random = random;
        _valid = valid;
    }

    public string Random()
    {
        if (_valid) return GenerateValid();

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < 4; i++)
        {
            sb.Append(_random.Next(10000).ToString("D4"));
            if (i < 3) sb.Append('-');
        }
        return sb.ToString();
    }

    private string GenerateValid()
    {
        var digits = new int[16];
        for (var i = 0; i < 15; i++) digits[i] = _random.Next(10);
        digits[15] = CalculateLuhnCheckDigit(digits);

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < 16; i++)
        {
            sb.Append(digits[i]);
            if ((i + 1) % 4 == 0 && i < 15) sb.Append('-');
        }
        return sb.ToString();
    }

    private static int CalculateLuhnCheckDigit(int[] digits)
    {
        var sum = 0;
        for (var i = 0; i < 15; i++)
        {
            var digit = digits[i];
            if (i % 2 == 0)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
        }
        return (10 - (sum % 10)) % 10;
    }

    public long PoolSize() => _valid ? 1000000000000000L : 10000L * 10000L * 10000L * 10000L;
}
