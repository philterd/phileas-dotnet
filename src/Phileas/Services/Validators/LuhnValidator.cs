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
///     Standard mod-10 Luhn checksum validator for the custom identifier filter. The check runs over
///     the digits of the matched text, ignoring separators, so a value may be formatted or
///     unformatted. Mirrors the Java <c>LuhnValidator</c>.
/// </summary>
public class LuhnValidator : ISpanValidator
{
    private static readonly LuhnValidator Singleton = new();

    private LuhnValidator()
    {
    }

    /// <summary>Returns the shared instance.</summary>
    public static ISpanValidator GetInstance() => Singleton;

    /// <inheritdoc />
    public bool Validate(Span span) => IsValid(span.Text);

    /// <summary>Runs the standard mod-10 Luhn checksum over the digits in <paramref name="text" />.</summary>
    public static bool IsValid(string? text)
    {
        if (text == null)
            return false;

        var sum = 0;
        var doubleDigit = false;
        var digitCount = 0;

        for (var i = text.Length - 1; i >= 0; i--)
        {
            var c = text[i];
            if (c < '0' || c > '9')
                continue;

            var digit = c - '0';
            digitCount++;

            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9)
                    digit -= 9;
            }

            sum += digit;
            doubleDigit = !doubleDigit;
        }

        return digitCount > 0 && sum % 10 == 0;
    }
}
