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
///     Damm check-digit validator for the custom identifier filter. The scheme uses a weakly totally
///     anti-symmetric quasigroup, catching every single-digit error and every transposition of
///     adjacent digits without needing a permutation table. The last digit of the value is the check
///     digit. Separators are ignored.
/// </summary>
public class DammValidator : ISpanValidator
{
    /// <summary>The quasigroup operation table.</summary>
    private static readonly int[][] Quasigroup =
    {
        new[] { 0, 3, 1, 7, 5, 9, 8, 6, 4, 2 },
        new[] { 7, 0, 9, 2, 1, 5, 4, 8, 6, 3 },
        new[] { 4, 2, 0, 6, 8, 7, 1, 3, 5, 9 },
        new[] { 1, 7, 5, 0, 9, 8, 3, 4, 2, 6 },
        new[] { 6, 1, 2, 3, 0, 4, 5, 9, 7, 8 },
        new[] { 3, 6, 7, 4, 2, 0, 9, 5, 8, 1 },
        new[] { 5, 8, 6, 9, 7, 2, 0, 1, 3, 4 },
        new[] { 8, 9, 4, 5, 3, 6, 2, 0, 1, 7 },
        new[] { 9, 4, 3, 8, 6, 1, 7, 2, 0, 5 },
        new[] { 2, 5, 8, 1, 4, 3, 6, 7, 9, 0 }
    };

    private static readonly DammValidator Singleton = new();

    private DammValidator()
    {
    }

    /// <summary>Returns the shared instance.</summary>
    public static ISpanValidator GetInstance() => Singleton;

    /// <inheritdoc />
    public bool Validate(Span span) => IsValid(span.Text);

    /// <summary>Runs the Damm check over the digits in <paramref name="text" />.</summary>
    public static bool IsValid(string? text)
    {
        if (text == null)
            return false;

        var interim = 0;
        var digitCount = 0;

        foreach (var c in text)
        {
            if (c < '0' || c > '9')
                continue;

            interim = Quasigroup[interim][c - '0'];
            digitCount++;
        }

        return digitCount > 0 && interim == 0;
    }
}
