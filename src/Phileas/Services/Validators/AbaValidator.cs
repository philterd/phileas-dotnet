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
///     ABA routing transit number checksum for the custom identifier filter: the 3-7-1 weighted sum of
///     the nine digits must be a multiple of 10. The check runs over the digits of the matched text,
///     ignoring separators, and requires exactly nine of them.
/// </summary>
public class AbaValidator : ISpanValidator
{
    private static readonly int[] Weights = { 3, 7, 1, 3, 7, 1, 3, 7, 1 };

    private static readonly AbaValidator Singleton = new();

    private AbaValidator()
    {
    }

    /// <summary>Returns the shared instance.</summary>
    public static ISpanValidator GetInstance() => Singleton;

    /// <inheritdoc />
    public bool Validate(Span span) => IsValid(span.Text);

    /// <summary>Runs the 3-7-1 mod 10 checksum over the digits in <paramref name="text" />.</summary>
    public static bool IsValid(string? text)
    {
        if (text == null)
            return false;

        var sum = 0;
        var digitCount = 0;

        foreach (var c in text)
        {
            if (c < '0' || c > '9')
                continue;

            // A tenth digit is not a routing number, so stop rather than wrap the weights around.
            if (digitCount == Weights.Length)
                return false;

            sum += (c - '0') * Weights[digitCount];
            digitCount++;
        }

        return digitCount == Weights.Length && sum % 10 == 0;
    }
}
