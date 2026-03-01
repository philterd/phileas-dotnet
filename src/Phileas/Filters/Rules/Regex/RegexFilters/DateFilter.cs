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
using Phileas.Filters;
using Phileas.Filters.Rules.Regex;
using Phileas.Model;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Rules.Regex.RegexFilters;

/// <summary>
/// Regex-based filter that detects date expression entities in plain text.
/// </summary>
public class DateFilter : RegexFilter
{
    private static readonly Analyzer DateAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b(0?[1-9]|1[012])[\/\-\.](0?[1-9]|[12][0-9]|3[01])[\/\-\.](19|20)?\d{2}\b").WithInitialConfidence(0.85).Build(),
        new FilterPattern.Builder().WithPattern(@"\b(January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},?\s+\d{4}\b", RegexOptions.IgnoreCase).WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder().WithPattern(@"\b\d{1,2}\s+(January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{4}\b", RegexOptions.IgnoreCase).WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder().WithPattern(@"\b(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[.\s]\s*\d{1,2},?\s*\d{4}\b", RegexOptions.IgnoreCase).WithInitialConfidence(0.85).Build()
    );

    /// <summary>
    /// Initializes a new <see cref="DateFilter"/> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    public DateFilter(FilterConfiguration configuration) : base(FilterType.Date, configuration) { }

    /// <inheritdoc/>
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, DateAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
