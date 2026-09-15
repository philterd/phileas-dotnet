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
using System.Text.RegularExpressions;
using Phileas.Model;
using Phileas.Policy;

namespace Phileas.Filters.Strategies.Rules;

/// <summary>
///     Runtime filter strategy for date expression detection. Supports all standard replacement strategies plus
///     <see cref="AbstractFilterStrategy.ShiftDate" />, which shifts the detected date by configured days, months,
///     and/or years while preserving the original date format.
/// </summary>
public class DateFilterStrategy : StandardFilterStrategy
{
    private static readonly Random Random = new();

    /// <summary>Gets or sets the number of days to add, or subtract if negative, when shifting.</summary>
    public int ShiftDays { get; set; } = 0;

    /// <summary>Gets or sets the number of months to add, or subtract if negative, when shifting.</summary>
    public int ShiftMonths { get; set; } = 0;

    /// <summary>Gets or sets the number of years to add, or subtract if negative, when shifting.</summary>
    public int ShiftYears { get; set; } = 0;

    /// <summary>Gets or sets whether to shift by a random amount rather than the configured offsets.</summary>
    public bool ShiftRandom { get; set; } = false;

    /// <summary>Gets or sets whether a shifted date may land in the future.</summary>
    public bool FutureDates { get; set; } = false;

    /// <inheritdoc />
    public override Replacement GetReplacement(string context, string token, string[] window, double confidence,
        string? classification, FilterPattern? filterPattern, Crypto? crypto, Fpe? fpe)
    {
        // Both names reach here: SHIFT is what the policy schema and the PhiSQL compiler emit, and
        // SHIFT_DATE is what this port has always accepted.
        if (Strategy == ShiftDate || Strategy == AbstractFilterStrategy.Shift)
        {
            var (days, months, years) = ShiftRandom
                ? (Random.Next(1, 30), Random.Next(1, 12), -Random.Next(1, 3))
                : (ShiftDays, ShiftMonths, ShiftYears);

            var shifted = ShiftDateValue(token, days, months, years, FutureDates);
            return new Replacement(shifted, string.Empty, shifted != token);
        }

        return GetStandardReplacement(context, token, window, confidence, classification, filterPattern, crypto, fpe,
            FilterType.Date);
    }

    private static string ShiftDateValue(string token, int days, int months, int years, bool futureDates)
    {
        if (!DateTime.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return token;

        var original = date;
        date = date.AddDays(days).AddMonths(months).AddYears(years);

        // A date that was in the past must stay there unless the policy allows otherwise, so the shift
        // is applied in the opposite direction rather than dropped: the magnitude the policy asked for
        // is preserved either way.
        if (!futureDates && date > DateTime.Today && original <= DateTime.Today)
            date = original.AddDays(-days).AddMonths(-months).AddYears(-years);

        // Numeric format: M/D/YYYY, M-D-YYYY, M.D.YYYY
        var numericMatch = Regex.Match(token, @"^(\d{1,2})([\/\-\.])(\d{1,2})\2(\d{2,4})$", RegexOptions.None,
            RegexDefaults.MatchTimeout);
        if (numericMatch.Success)
        {
            var sep = numericMatch.Groups[2].Value;
            var yearStr = numericMatch.Groups[4].Value.Length == 2
                ? date.Year.ToString(CultureInfo.InvariantCulture)[2..]
                : date.Year.ToString(CultureInfo.InvariantCulture);
            return $"{date.Month}{sep}{date.Day}{sep}{yearStr}";
        }

        // Full month name, day, year: "January 15, 1990"
        if (Regex.IsMatch(token,
                @"^(January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},?\s+\d{4}$",
                RegexOptions.IgnoreCase, RegexDefaults.MatchTimeout))
        {
            var comma = token.Contains(',') ? "," : "";
            return $"{date.ToString("MMMM", CultureInfo.InvariantCulture)} {date.Day}{comma} {date.Year}";
        }

        // Day, full month name, year: "15 January 1990"
        if (Regex.IsMatch(token,
                @"^\d{1,2}\s+(January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{4}$",
                RegexOptions.IgnoreCase, RegexDefaults.MatchTimeout))
            return $"{date.Day} {date.ToString("MMMM", CultureInfo.InvariantCulture)} {date.Year}";

        // Abbreviated month, day, year: "Jan. 5, 2023" or "Jan 5, 2023"
        if (Regex.IsMatch(token,
                @"^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[.\s]\s*\d{1,2},?\s*\d{4}$",
                RegexOptions.IgnoreCase, RegexDefaults.MatchTimeout))
        {
            var dotAfterMonth = Regex.IsMatch(token, @"^[A-Za-z]{3}\.", RegexOptions.None, RegexDefaults.MatchTimeout);
            var comma = token.Contains(',') ? "," : "";
            var monthAbbr = date.ToString("MMM", CultureInfo.InvariantCulture);
            return $"{monthAbbr}{(dotAfterMonth ? ". " : " ")}{date.Day}{comma} {date.Year}";
        }

        // Default fallback
        return date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
    }
}