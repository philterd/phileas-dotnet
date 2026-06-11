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

using System.Globalization;
using Phileas.Model;

namespace Phileas.Services.Validators;

/// <summary>
///     Validates that a date span's text parses strictly against the span's date format and that the
///     year is plausible (1800-2200). Mirrors the Java <c>DateSpanValidator</c>.
/// </summary>
public class DateSpanValidator : ISpanValidator
{
    private static readonly DateSpanValidator Singleton = new();

    private DateSpanValidator()
    {
        // Use the static GetInstance().
    }

    /// <summary>Returns the shared instance.</summary>
    public static ISpanValidator GetInstance() => Singleton;

    /// <inheritdoc />
    public bool Validate(Span span)
    {
        if (span.Pattern == null || span.Text.Length == 0) return false;

        // Java uses "u" for the year in strict mode; .NET uses "y".
        var format = span.Pattern.Replace('u', 'y');

        if (!DateTime.TryParseExact(span.Text, format, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        // If it's a 2-digit year add 2000, then sanity-check the year.
        var year = parsed.Year;
        if (year < 100) year += 2000;
        return year is >= 1800 and <= 2200;
    }
}
