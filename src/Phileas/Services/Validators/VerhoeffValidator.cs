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
///     Verhoeff check-digit validator for the custom identifier filter. The scheme works in the
///     dihedral group D5, which catches every single-digit error and every transposition of adjacent
///     digits. The last digit of the value is the check digit. Separators are ignored.
/// </summary>
public class VerhoeffValidator : ISpanValidator
{
    /// <summary>The D5 multiplication table.</summary>
    private static readonly int[][] Multiplication =
    {
        new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
        new[] { 1, 2, 3, 4, 0, 6, 7, 8, 9, 5 },
        new[] { 2, 3, 4, 0, 1, 7, 8, 9, 5, 6 },
        new[] { 3, 4, 0, 1, 2, 8, 9, 5, 6, 7 },
        new[] { 4, 0, 1, 2, 3, 9, 5, 6, 7, 8 },
        new[] { 5, 9, 8, 7, 6, 0, 4, 3, 2, 1 },
        new[] { 6, 5, 9, 8, 7, 1, 0, 4, 3, 2 },
        new[] { 7, 6, 5, 9, 8, 2, 1, 0, 4, 3 },
        new[] { 8, 7, 6, 5, 9, 3, 2, 1, 0, 4 },
        new[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 }
    };

    /// <summary>The permutation table, applied cyclically by digit position.</summary>
    private static readonly int[][] Permutation =
    {
        new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
        new[] { 1, 5, 7, 6, 2, 8, 3, 0, 9, 4 },
        new[] { 5, 8, 0, 3, 7, 9, 6, 1, 4, 2 },
        new[] { 8, 9, 1, 6, 0, 4, 3, 5, 2, 7 },
        new[] { 9, 4, 5, 3, 1, 2, 6, 8, 7, 0 },
        new[] { 4, 2, 8, 6, 5, 7, 3, 9, 0, 1 },
        new[] { 2, 7, 9, 3, 8, 0, 6, 4, 1, 5 },
        new[] { 7, 0, 4, 6, 9, 1, 3, 2, 5, 8 }
    };

    private static readonly VerhoeffValidator Singleton = new();

    private VerhoeffValidator()
    {
    }

    /// <summary>Returns the shared instance.</summary>
    public static ISpanValidator GetInstance() => Singleton;

    /// <inheritdoc />
    public bool Validate(Span span) => IsValid(span.Text);

    /// <summary>Runs the Verhoeff check over the digits in <paramref name="text" />.</summary>
    public static bool IsValid(string? text)
    {
        if (text == null)
            return false;

        var digits = Digits(text);
        if (digits.Count == 0)
            return false;

        var check = 0;
        for (var i = 0; i < digits.Count; i++)
        {
            // Digits are consumed right to left, the check digit first.
            var digit = digits[digits.Count - 1 - i];
            check = Multiplication[check][Permutation[i % 8][digit]];
        }

        return check == 0;
    }

    private static List<int> Digits(string text)
    {
        var digits = new List<int>(text.Length);
        foreach (var c in text)
        {
            if (c >= '0' && c <= '9')
                digits.Add(c - '0');
        }

        return digits;
    }
}
