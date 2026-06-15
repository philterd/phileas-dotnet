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

using System.Text.RegularExpressions;
using Phileas.Model;

namespace Phileas.Services.Validators;

/// <summary>
///     German tax identification number (Steuer-ID / IdNr) validator: applies the structural
///     digit-repetition rule on the first ten digits and the ISO/IEC 7064 MOD 11,10 check digit.
///     Mirrors the Java <c>DeSteuerIdValidator</c>.
/// </summary>
public partial class DeSteuerIdValidator : ISpanValidator
{
    private static readonly DeSteuerIdValidator Singleton = new();

    private DeSteuerIdValidator()
    {
    }

    /// <summary>Returns the shared instance.</summary>
    public static ISpanValidator GetInstance() => Singleton;

    /// <inheritdoc />
    public bool Validate(Span span) => IsValid(span.Text);

    /// <summary>Validates a German Steuer-ID.</summary>
    public static bool IsValid(string? text)
    {
        if (text == null)
            return false;

        var digits = SeparatorRegex().Replace(text, string.Empty);
        if (digits.Length != 11 || !digits.All(c => c is >= '0' and <= '9'))
            return false;

        if (digits[0] == '0')
            return false;

        if (!HasValidRepetition(digits[..10]))
            return false;

        return CheckDigit(digits[..10]) == digits[10] - '0';
    }

    private static bool HasValidRepetition(string firstTen)
    {
        var counts = new int[10];
        foreach (var c in firstTen)
            counts[c - '0']++;

        var twice = counts.Count(c => c == 2);
        var thrice = counts.Count(c => c == 3);

        if (counts.Any(c => c > 3))
            return false;

        return (twice == 1 && thrice == 0) || (twice == 0 && thrice == 1);
    }

    private static int CheckDigit(string firstTen)
    {
        var product = 10;
        foreach (var c in firstTen)
        {
            var s = (c - '0' + product) % 10;
            if (s == 0)
                s = 10;
            product = s * 2 % 11;
        }

        return (11 - product) % 10;
    }

    [GeneratedRegex(@"[\s./-]")]
    private static partial Regex SeparatorRegex();
}
