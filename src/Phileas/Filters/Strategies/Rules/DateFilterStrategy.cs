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
        if (Is(ShiftDate) || Is(AbstractFilterStrategy.Shift))
        {
            var (days, months, years) = ShiftRandom
                ? (Random.Next(1, 30), Random.Next(1, 12), -Random.Next(1, 3))
                : (ShiftDays, ShiftMonths, ShiftYears);

            var shifted = ShiftDateValue(token, days, months, years, FutureDates);
            return new Replacement(shifted, string.Empty, shifted != token);
        }

        if (Is(AbstractFilterStrategy.TruncateToYear))
        {
            var truncated = TruncateToYearValue(token, classification);
            return new Replacement(truncated, string.Empty, truncated != token);
        }

        if (Is(AbstractFilterStrategy.Relative))
        {
            var relative = RelativeValue(token, classification, FutureDates);
            return new Replacement(relative, string.Empty, relative != token);
        }

        // A name this build does not implement is a policy error rather than a reason to redact:
        // silently substituting redaction gave the policy author a destroyed date instead of the
        // year or the interval they asked for, with nothing to tell them. Mirrors the identifier
        // validators, which have always raised on a name they do not recognise.
        if (!IsKnownStrategy(Strategy))
            throw new ArgumentException(
                $"Unsupported date filter strategy '{Strategy}'. The date filter accepts: "
                + string.Join(", ", KnownStrategies) + ".");

        return GetStandardReplacement(context, token, window, confidence, classification, filterPattern, crypto, fpe,
            FilterType.Date);
    }

    /// <summary>The strategy names a date filter accepts, in the order they are documented.</summary>
    private static readonly string[] KnownStrategies =
    {
        Redact, RandomReplace, StaticReplace, CryptoReplace, FpeEncryptReplace, HashSha256Replace,
        Last4, Mask, Abbreviate, MapReplace, Same, Truncate,
        AbstractFilterStrategy.TruncateToYear, AbstractFilterStrategy.Shift, ShiftDate,
        AbstractFilterStrategy.Relative
    };

    /// <summary>Whether the configured strategy is <paramref name="name" />, ignoring case.</summary>
    private bool Is(string name)
    {
        return string.Equals(Strategy, name, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKnownStrategy(string? strategy)
    {
        return string.IsNullOrEmpty(strategy)
               || KnownStrategies.Any(k => string.Equals(k, strategy, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Replaces a parsed date with its year; an unparseable token falls back to redaction.</summary>
    private string TruncateToYearValue(string token, string? classification)
    {
        return DateTime.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.Year.ToString(CultureInfo.InvariantCulture)
            : GetRedactedToken(token, classification, FilterType.Date);
    }

    /// <summary>
    ///     Replaces a parsed date with a readable interval from today, matching the Java filter's
    ///     phrasing: <c>3 months ago</c>, or <c>2 years 1 months ago</c> once a year has passed. A date
    ///     ahead of today is phrased <c>in N months</c> when <c>futureDates</c> is on, and redacted when
    ///     it is off. An unparseable token falls back to redaction.
    /// </summary>
    private string RelativeValue(string token, string? classification, bool futureDates)
    {
        if (!DateTime.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return GetRedactedToken(token, classification, FilterType.Date);

        var (years, months, days) = PeriodBetween(date.Date, DateTime.Today);

        if (years >= 0 && months >= 0 && days >= 0)
        {
            // Java rounds up to the next month from the fifteenth day onward.
            var wholeMonths = days >= 15 ? months + 1 : months;
            return years == 0
                ? $"{wholeMonths} months ago"
                : $"{years} years {wholeMonths} months ago";
        }

        if (!futureDates)
            return GetRedactedToken(token, classification, FilterType.Date);

        // The day rounding is not applied here, matching the Java filter: the remaining days of a
        // future period are negative, so its "fifteenth day" test never fires.
        var futureMonths = Math.Abs(months);
        var futureYears = Math.Abs(years);
        return futureYears == 0
            ? $"in {futureMonths} months"
            : $"in {futureYears} years {futureMonths} months";
    }

    /// <summary>
    ///     Decomposes the span between two dates into years, months and days, as Java's
    ///     <c>Period.between</c> does. All three carry the sign of the span.
    /// </summary>
    private static (int Years, int Months, int Days) PeriodBetween(DateTime from, DateTime to)
    {
        var years = to.Year - from.Year;
        var months = to.Month - from.Month;
        var days = to.Day - from.Day;

        if (from <= to)
        {
            if (days < 0)
            {
                months--;
                days += DateTime.DaysInMonth(to.AddMonths(-1).Year, to.AddMonths(-1).Month);
            }

            if (months < 0)
            {
                years--;
                months += 12;
            }
        }
        else
        {
            if (days > 0)
            {
                months++;
                days -= DateTime.DaysInMonth(from.AddMonths(-1).Year, from.AddMonths(-1).Month);
            }

            if (months > 0)
            {
                years++;
                months -= 12;
            }
        }

        return (years, months, days);
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