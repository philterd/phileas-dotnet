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
    /// <summary>Gets or sets the number of days to add (or subtract if negative) when using <see cref="AbstractFilterStrategy.ShiftDate" />.</summary>
    public int Days { get; set; } = 0;

    /// <summary>Gets or sets the number of months to add (or subtract if negative) when using <see cref="AbstractFilterStrategy.ShiftDate" />.</summary>
    public int Months { get; set; } = 0;

    /// <summary>Gets or sets the number of years to add (or subtract if negative) when using <see cref="AbstractFilterStrategy.ShiftDate" />.</summary>
    public int Years { get; set; } = 0;

    /// <inheritdoc />
    public override Replacement GetReplacement(string context, string token, string[] window, double confidence,
        string? classification, FilterPattern? filterPattern, Crypto? crypto, Fpe? fpe)
    {
        if (Strategy == ShiftDate)
        {
            var shifted = ShiftDateValue(token, Days, Months, Years);
            return new Replacement(shifted, string.Empty, shifted != token);
        }

        return GetStandardReplacement(context, token, window, confidence, classification, filterPattern, crypto, fpe,
            FilterType.Date);
    }

    private static string ShiftDateValue(string token, int days, int months, int years)
    {
        if (!DateTime.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return token;

        date = date.AddDays(days).AddMonths(months).AddYears(years);

        // Numeric format: M/D/YYYY, M-D-YYYY, M.D.YYYY
        var numericMatch = Regex.Match(token, @"^(\d{1,2})([\/\-\.])(\d{1,2})\2(\d{2,4})$");
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
                RegexOptions.IgnoreCase))
        {
            var comma = token.Contains(',') ? "," : "";
            return $"{date.ToString("MMMM", CultureInfo.InvariantCulture)} {date.Day}{comma} {date.Year}";
        }

        // Day, full month name, year: "15 January 1990"
        if (Regex.IsMatch(token,
                @"^\d{1,2}\s+(January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{4}$",
                RegexOptions.IgnoreCase))
            return $"{date.Day} {date.ToString("MMMM", CultureInfo.InvariantCulture)} {date.Year}";

        // Abbreviated month, day, year: "Jan. 5, 2023" or "Jan 5, 2023"
        if (Regex.IsMatch(token,
                @"^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[.\s]\s*\d{1,2},?\s*\d{4}$",
                RegexOptions.IgnoreCase))
        {
            var dotAfterMonth = Regex.IsMatch(token, @"^[A-Za-z]{3}\.");
            var comma = token.Contains(',') ? "," : "";
            var monthAbbr = date.ToString("MMM", CultureInfo.InvariantCulture);
            return $"{monthAbbr}{(dotAfterMonth ? ". " : " ")}{date.Day}{comma} {date.Year}";
        }

        // Default fallback
        return date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
    }
}