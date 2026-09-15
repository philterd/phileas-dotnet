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

            var shifted = ShiftDateValue(token, filterPattern, days, months, years, FutureDates);

            // A detected date that cannot be parsed is still PHI, so it is redacted rather than
            // returned as it was. Returning the token left the value in the document, which is the
            // one outcome a date strategy must never produce. Matches the Java filter.
            return shifted == null
                ? new Replacement(GetRedactedToken(token, classification, FilterType.Date), string.Empty)
                : new Replacement(shifted, string.Empty, shifted != token);
        }

        if (Is(AbstractFilterStrategy.TruncateToYear))
        {
            var truncated = TruncateToYearValue(token, filterPattern, classification);
            return new Replacement(truncated, string.Empty, truncated != token);
        }

        if (Is(AbstractFilterStrategy.Relative))
        {
            var relative = RelativeValue(token, filterPattern, classification, FutureDates);
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

    /// <summary>
    ///     Parses a detected date, preferring the format recorded on the pattern that matched it.
    ///     <para>
    ///         The format is what says which of the leading numbers is the day, so without it
    ///         <c>15/01/1990</c> is read month first, fails, and the date is lost. The month-name
    ///         patterns carry no format and fall back to the invariant culture, which reads them.
    ///     </para>
    /// </summary>
    /// <param name="token">The detected text.</param>
    /// <param name="filterPattern">The pattern that produced the match, or <see langword="null" />.</param>
    /// <param name="date">The parsed date.</param>
    /// <param name="format">
    ///     The format the date was parsed with, or <see langword="null" /> when the invariant culture
    ///     read it. Writing the date back with this format reproduces the original ordering,
    ///     separators and padding.
    /// </param>
    /// <returns><see langword="true" /> when the token parsed as a date.</returns>
    private static bool TryParseToken(string token, FilterPattern? filterPattern, out DateTime date,
        out string? format)
    {
        // Java writes the year as 'u' and .NET as 'y'; DateSpanValidator makes the same substitution.
        format = string.IsNullOrEmpty(filterPattern?.Format) ? null : filterPattern.Format.Replace('u', 'y');

        if (format != null && DateTime.TryParseExact(token, format, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date))
            return true;

        format = null;
        return DateTime.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    /// <summary>Replaces a parsed date with its year; an unparseable token falls back to redaction.</summary>
    private string TruncateToYearValue(string token, FilterPattern? filterPattern, string? classification)
    {
        return TryParseToken(token, filterPattern, out var date, out _)
            ? date.Year.ToString(CultureInfo.InvariantCulture)
            : GetRedactedToken(token, classification, FilterType.Date);
    }

    /// <summary>
    ///     Replaces a parsed date with a readable interval from today, matching the Java filter's
    ///     phrasing: <c>3 months ago</c>, or <c>2 years 1 months ago</c> once a year has passed. A date
    ///     ahead of today is phrased <c>in N months</c> when <c>futureDates</c> is on, and redacted when
    ///     it is off. An unparseable token falls back to redaction.
    /// </summary>
    private string RelativeValue(string token, FilterPattern? filterPattern, string? classification,
        bool futureDates)
    {
        if (!TryParseToken(token, filterPattern, out var date, out _))
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

    /// <summary>
    ///     Shifts a detected date, preserving how it was written. Returns <see langword="null" /> when
    ///     the token cannot be parsed, which the caller turns into a redaction.
    /// </summary>
    private static string? ShiftDateValue(string token, FilterPattern? filterPattern, int days, int months,
        int years, bool futureDates)
    {
        if (!TryParseToken(token, filterPattern, out var date, out var format))
            return null;

        var original = date;
        date = date.AddDays(days).AddMonths(months).AddYears(years);

        // A date that was in the past must stay there unless the policy allows otherwise, so the shift
        // is applied in the opposite direction rather than dropped: the magnitude the policy asked for
        // is preserved either way.
        if (!futureDates && date > DateTime.Today && original <= DateTime.Today)
            date = original.AddDays(-days).AddMonths(-months).AddYears(-years);

        // Written back with the format it was read with, so the ordering, separators and padding of
        // the original are reproduced exactly. Every numeric pattern carries a format, so the branches
        // below are the no-format path: the month-name patterns, and a caller that supplies no pattern.
        if (format != null)
            return date.ToString(format, CultureInfo.InvariantCulture);

        // Year-first numeric format: YYYY-MM-DD, YYYY/MM/DD, YYYY.MM.DD. Checked first because the
        // month-first pattern below would otherwise have to be read to see that it cannot match.
        var yearFirstMatch = Regex.Match(token, @"^\d{4}([\/\-\.])(\d{2})\1(\d{2})$", RegexOptions.None,
            RegexDefaults.MatchTimeout);
        if (yearFirstMatch.Success)
        {
            var sep = yearFirstMatch.Groups[1].Value;
            return $"{date.Year:D4}{sep}{date.Month:D2}{sep}{date.Day:D2}";
        }

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

        // Day, month name, year: "15 January 1990", "15 Jan 1990", "15-Jan-1990", "15/Jan/1990".
        var dayFirstNamedMatch = Regex.Match(token,
            @"^(?<day>\d{1,2})(?<sep>\s*[\-\/.]\s*|\s+)"
            + @"(?<month>January|February|March|April|May|June|July|August|September|October|November|December"
            + @"|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sep|Oct|Nov|Dec)(?<dot>\.?)\k<sep>\d{4}$",
            RegexOptions.IgnoreCase, RegexDefaults.MatchTimeout);
        if (dayFirstNamedMatch.Success)
        {
            var sep = dayFirstNamedMatch.Groups["sep"].Value;
            // "May" is both the full name and the abbreviation, so either branch prints the same text.
            var abbreviated = dayFirstNamedMatch.Groups["month"].Value.Length <= 3;
            var monthName = date.ToString(abbreviated ? "MMM" : "MMMM", CultureInfo.InvariantCulture);
            var dayText = dayFirstNamedMatch.Groups["day"].Value.Length == 2
                ? date.Day.ToString("D2", CultureInfo.InvariantCulture)
                : date.Day.ToString(CultureInfo.InvariantCulture);
            return $"{dayText}{sep}{monthName}{dayFirstNamedMatch.Groups["dot"].Value}{sep}{date.Year}";
        }

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