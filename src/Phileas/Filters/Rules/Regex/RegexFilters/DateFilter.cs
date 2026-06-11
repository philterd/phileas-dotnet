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
///     Regex-based filter that detects date expression entities in plain text. When
///     <c>onlyValidDates</c> is enabled, numeric dates that do not parse as real calendar dates (for
///     example <c>02-31-2019</c>) are discarded; month-name dates are always treated as valid.
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
        }

        // Month-name dates are specific enough that they are always treated as valid dates.
        patterns.Add(new FilterPattern.Builder()
            .WithPattern($@"\b({monthNames})\s+\d{{1,2}},?\s+\d{{4}}\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).WithAlwaysValid(true).Build());

        patterns.Add(new FilterPattern.Builder()
            .WithPattern($@"\b\d{{1,2}}\s+({monthNames})\s+\d{{4}}\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).WithAlwaysValid(true).Build());

        patterns.Add(new FilterPattern.Builder()
            .WithPattern($@"\b({monthAbbreviations})[.\s]\s*\d{{1,2}},?\s*\d{{4}}\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.85).WithAlwaysValid(true).Build());

        return patterns.ToArray();
    }
}
