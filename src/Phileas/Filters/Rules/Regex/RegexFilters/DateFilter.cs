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
using Phileas.Services.Validators;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Rules.Regex.RegexFilters;

/// <summary>
///     Regex-based filter that detects date expression entities in plain text. Numeric dates are
///     detected month first (<c>01/15/1990</c>), day first (<c>15/01/1990</c>) and year first
///     (<c>1990-01-15</c>), each with <c>/</c>, <c>-</c> or <c>.</c> as the delimiter, and month-name
///     dates with the month or the day leading. A year-first date is also detected as the date part of
///     an ISO 8601 timestamp, where the time is left in the document.
///     <para>
///         When <c>onlyValidDates</c> is enabled, numeric dates that do not parse as real calendar
///         dates (for example <c>02-31-2019</c>) are discarded; month-name dates are always treated
///         as valid.
///     </para>
/// </summary>
public class DateFilter : RegexFilter
{
    private static readonly Analyzer DateAnalyzer = new(BuildPatterns());

    private readonly bool _onlyValidDates;
    private readonly ISpanValidator _spanValidator;

    /// <summary>
    ///     Initializes a new <see cref="DateFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    /// <param name="onlyValidDates">When <see langword="true" />, numeric dates that are not real calendar dates are dropped.</param>
    /// <param name="spanValidator">The validator used to check numeric dates; defaults to <see cref="DateSpanValidator" />.</param>
    public DateFilter(FilterConfiguration configuration, bool onlyValidDates = false,
        ISpanValidator? spanValidator = null) : base(FilterType.Date, configuration)
    {
        _onlyValidDates = onlyValidDates;
        _spanValidator = spanValidator ?? DateSpanValidator.GetInstance();
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, DateAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);

        if (_onlyValidDates)
            // A span is kept only when its pattern is always valid (the month-name patterns) or the
            // date parses against the span's format. Mirrors the Java DateFilter.
            spans = spans.Where(span => span.AlwaysValid || _spanValidator.Validate(span)).ToList();

        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }

    private static FilterPattern[] BuildPatterns()
    {
        const string month = @"(0?[1-9]|1[012])";
        const string day = @"(0?[1-9]|[12][0-9]|3[01])";
        const string monthNames =
            "January|February|March|April|May|June|July|August|September|October|November|December";
        const string monthAbbreviations = "Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec";

        // Year-first dates take a zero-padded month and day, as ISO 8601 requires and as the Java
        // filter does. Accepting a single digit there would make a version string such as 2020.1.5 a
        // date, which the leading (19|20) does nothing to rule out.
        const string paddedMonth = "(0[1-9]|1[012])";
        const string paddedDay = "(0[1-9]|[12][0-9]|3[01])";

        // A date ends on a word boundary, or on the T that introduces the time of an ISO 8601
        // timestamp: a word boundary alone would reject 2024-06-01T09:30:00Z entirely, and a log or
        // an export writes the timestamp far more often than the bare date. Anything else adjacent
        // still ends the date, so 1990-01-15x is not one.
        const string isoDateEnd = @"(?:\b|(?=[Tt]\d))";

        // A day and a month name are separated by whitespace or by one of the delimiters the numeric
        // patterns accept. The separator is captured once and backreferenced so both sides agree,
        // rather than assembling a date out of "15-January 1990".
        const string nameSeparator = @"(?<sep>\s*[\-\/.]\s*|\s+)";

        var patterns = new List<FilterPattern>();

        // Numeric dates with a delimiter. Each delimiter and year-length combination is its own pattern so
        // it can carry the exact date format used to validate the match when onlyValidDates is enabled.
        foreach (var (regexDelimiter, formatDelimiter) in new[] { (@"\/", "/"), (@"\-", "-"), (@"\.", ".") })
        {
            patterns.Add(new FilterPattern.Builder()
                .WithPattern($@"\b{month}{regexDelimiter}{day}{regexDelimiter}(19|20)\d{{2}}\b")
                .WithInitialConfidence(0.85)
                .WithFormat($"M{formatDelimiter}d{formatDelimiter}yyyy")
                .Build());

            patterns.Add(new FilterPattern.Builder()
                .WithPattern($@"\b{month}{regexDelimiter}{day}{regexDelimiter}\d{{2}}\b")
                .WithInitialConfidence(0.85)
                .WithFormat($"M{formatDelimiter}d{formatDelimiter}yy")
                .Build());

            // Day-first numeric dates (e.g. 25/12/1980). The month-first patterns above cannot match
            // these (the leading day exceeds the month group's max of 12), and these cannot match
            // month-first-only dates (the middle month value would exceed 12), so an unambiguous date
            // matches exactly one ordering. Genuinely ambiguous dates (e.g. 03/04/1981) match both and
            // are collapsed by DropOverlappingSpans.
            patterns.Add(new FilterPattern.Builder()
                .WithPattern($@"\b{day}{regexDelimiter}{month}{regexDelimiter}(19|20)\d{{2}}\b")
                .WithInitialConfidence(0.85)
                .WithFormat($"d{formatDelimiter}M{formatDelimiter}yyyy")
                .Build());

            patterns.Add(new FilterPattern.Builder()
                .WithPattern($@"\b{day}{regexDelimiter}{month}{regexDelimiter}\d{{2}}\b")
                .WithInitialConfidence(0.85)
                .WithFormat($"d{formatDelimiter}M{formatDelimiter}yy")
                .Build());

            // Year-first numeric dates (1990-01-15 and the same shape with the other delimiters).
            // ISO 8601 is the ordinary machine-written form and appears in exports, logs and clinical
            // extracts. No month-first or day-first pattern can match the same text, since neither
            // leading group accepts four digits, so the orderings stay unambiguous.
            patterns.Add(new FilterPattern.Builder()
                .WithPattern(
                    $@"\b(19|20)\d{{2}}{regexDelimiter}{paddedMonth}{regexDelimiter}{paddedDay}{isoDateEnd}")
                .WithInitialConfidence(0.85)
                .WithFormat($"yyyy{formatDelimiter}MM{formatDelimiter}dd")
                .Build());
        }

        // Month-name dates are specific enough that they are always treated as valid dates.
        patterns.Add(new FilterPattern.Builder()
            .WithPattern($@"\b({monthNames})\s+\d{{1,2}},?\s+\d{{4}}\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).WithAlwaysValid(true).Build());

        // Day-first month-name dates: 15 January 1990, 15 Jan 1990, 15-Jan-1990, 15/Jan/1990. An
        // abbreviation may carry a trailing period, which the backreferenced separator would
        // otherwise reject.
        patterns.Add(new FilterPattern.Builder()
            .WithPattern($@"\b{day}{nameSeparator}(?:{monthNames}|{monthAbbreviations})\.?\k<sep>\d{{4}}\b",
                RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).WithAlwaysValid(true).Build());

        patterns.Add(new FilterPattern.Builder()
            .WithPattern($@"\b({monthAbbreviations})[.\s]\s*\d{{1,2}},?\s*\d{{4}}\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.85).WithAlwaysValid(true).Build());

        return patterns.ToArray();
    }
}
