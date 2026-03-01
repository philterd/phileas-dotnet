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

namespace Phileas.Filters.Rules.Regex;

/// <summary>
/// Abstract base for filters that use regular-expression patterns to detect entities.
/// Delegates pattern matching to an <see cref="Phileas.Filters.Analyzer"/> and exposes a
/// <see cref="FindSpans"/> helper that handles match extraction, ignore checking, windowing,
/// and replacement computation.
/// </summary>
public abstract class RegexFilter : RulesFilter
{
    /// <summary>Gets or sets the <see cref="Phileas.Filters.Analyzer"/> that holds the patterns for this filter.</summary>
    protected Analyzer? Analyzer { get; set; }

    /// <summary>
    /// Initializes the regex filter with the given type and configuration.
    /// </summary>
    /// <param name="filterType">The entity type handled by this filter.</param>
    /// <param name="configuration">Runtime filter configuration.</param>
    protected RegexFilter(FilterType filterType, FilterConfiguration configuration)
        : base(filterType, configuration) { }

    /// <summary>
    /// Runs the <paramref name="analyzer"/> patterns against <paramref name="input"/> and returns
    /// a list of <see cref="Span"/> objects for all accepted matches.
    /// </summary>
    /// <param name="policy">The active policy (used to check whether the filter is enabled and for replacement decisions).</param>
    /// <param name="analyzer">The <see cref="Phileas.Filters.Analyzer"/> containing the patterns to run.</param>
    /// <param name="input">The plain-text string to search.</param>
    /// <param name="context">The context identifier.</param>
    /// <param name="piece">Zero-based piece index within a multi-part document.</param>
    /// <returns>A list of <see cref="Span"/> objects describing each accepted match.</returns>
    protected IList<Span> FindSpans(Phileas.Policy.Policy policy, Analyzer analyzer, string input, string context, int piece)
    {
        var spans = new List<Span>();

        if (!policy.Identifiers.HasFilter(FilterType)) return spans;

        foreach (var filterPattern in analyzer.FilterPatterns)
        {
            var matches = filterPattern.Pattern.Matches(input);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                string matchText;
                int matchStart;
                int matchEnd;

                if (filterPattern.GroupNumber > 0 && match.Groups.Count > filterPattern.GroupNumber)
                {
                    var group = match.Groups[filterPattern.GroupNumber];
                    matchText = group.Value;
                    matchStart = group.Index;
                    matchEnd = group.Index + group.Length;
                }
                else
                {
                    matchText = match.Value;
                    matchStart = match.Index;
                    matchEnd = match.Index + match.Length;
                }

                if (string.IsNullOrWhiteSpace(matchText)) continue;
                if (IsIgnored(matchText)) continue;
                if (Span.DoesSpanExist(matchStart, matchEnd, spans)) continue;

                var window = GetWindow(input, matchStart, matchEnd);
                var confidence = filterPattern.InitialConfidence;

                var replacement = GetReplacement(policy, context, matchText, window, confidence, filterPattern.Classification ?? Classification, filterPattern);

                var span = Span.Make(
                    matchStart, matchEnd,
                    FilterType, context, confidence, matchText,
                    replacement.Value, replacement.Salt,
                    false, replacement.Applied,
                    window, Priority);
                span.AlwaysValid = filterPattern.AlwaysValid;
                span.Classification = filterPattern.Classification ?? Classification;

                spans.Add(span);
            }
        }

        return spans;
    }
}
