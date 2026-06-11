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
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Rules.Regex.RegexFilters;

/// <summary>Redacts everything from a start pattern through an end pattern (inclusive). Mirrors the Java <c>SectionFilter</c>.</summary>
public class SectionFilter : RegexFilter
{
    private readonly Analyzer _analyzer;

    /// <summary>Creates a section filter delimited by the given start and end patterns.</summary>
    public SectionFilter(FilterConfiguration configuration, string startPattern, string endPattern)
        : base(FilterType.Section, configuration)
    {
        // Wrap each sub-pattern in a non-capturing group so it cannot alter the combined regex's group
        // structure. The whole match (markers included) is the redacted span. The sub-patterns are
        // user-supplied, so the match time is bounded by the configured regex budget.
        var pattern = new System.Text.RegularExpressions.Regex(
            "(?:" + startPattern + ")(.*?)(?:" + endPattern + ")",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromMilliseconds(configuration.RegexTimeoutMs));
        var filterPattern = new FilterPattern.Builder()
            .WithPattern(pattern)
            .WithInitialConfidence(0.90)
            .Build();
        _analyzer = new Analyzer(filterPattern);
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        return new Filtered(context, piece, FindSpans(policy, _analyzer, input, context, piece));
    }
}
