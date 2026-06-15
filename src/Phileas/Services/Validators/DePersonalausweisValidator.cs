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

using Phileas.Model;

namespace Phileas.Services.Validators;

/// <summary>
///     German Personalausweis (ID card) number validator: validates the ICAO 9303 7-3-1 weighted
///     check digit over the 9-character document number. Mirrors the Java
///     <c>DePersonalausweisValidator</c>.
/// </summary>
public class DePersonalausweisValidator : ISpanValidator
{
    private static readonly int[] Weights = [7, 3, 1];

    private static readonly DePersonalausweisValidator Singleton = new();

    private DePersonalausweisValidator()
    {
    }

    /// <summary>Returns the shared instance.</summary>
    public static ISpanValidator GetInstance() => Singleton;

    /// <inheritdoc />
    public bool Validate(Span span) => IsValid(span.Text);

    /// <summary>Validates the ICAO 9303 7-3-1 check digit of a 10-character German ID card number.</summary>
    public static bool IsValid(string? text)
    {
        if (text == null)
            return false;

        var s = text.Trim().ToUpperInvariant();
        if (s.Length != 10)
            return false;

        var checkChar = s[9];
        if (checkChar is < '0' or > '9')
            return false;

        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var value = CharValue(s[i]);
            if (value < 0)
                return false;
            sum += value * Weights[i % 3];
        }

        return sum % 10 == checkChar - '0';
    }

    private static int CharValue(char c)
    {
        if (c is >= '0' and <= '9')
            return c - '0';
        if (c is >= 'A' and <= 'Z')
            return 10 + (c - 'A');
        return -1;
    }
}
