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

using Phileas.Filters.PostFilters;
using Phileas.Model;

namespace Phileas.Filters.Rules;

/// <summary>
///     Intermediate base class between <see cref="AbstractFilter" /> and concrete rule-based filters.
///     Provides the <see cref="PostFilter" /> hook that concrete filters can override to refine
///     detected spans after the initial matching pass.
/// </summary>
public abstract class RulesFilter : AbstractFilter
{
    /// <summary>
    ///     Initializes the rules filter with the given type and configuration.
    /// </summary>
    /// <param name="filterType">The entity type handled by this filter.</param>
    /// <param name="configuration">Runtime filter configuration.</param>
    protected RulesFilter(FilterType filterType, FilterConfiguration configuration)
        : base(filterType, configuration)
    {
    }

    /// <summary>
    ///     Applies post-processing rules to refine or remove spans after initial matching.
    ///     The default implementation returns the spans unchanged.
    /// </summary>
    /// <param name="spans">The list of spans produced by the initial matching pass.</param>
    /// <param name="input">The original input text.</param>
    /// <returns>The refined list of spans.</returns>
    protected IList<Span> PostFilter(IList<Span> spans, string input)
    {
        if (PostFiltersConfig.RemoveTrailingNewLines)
            spans = TrailingNewLinesPostFilter.Apply(spans);
        if (PostFiltersConfig.RemoveTrailingPeriods)
            spans = TrailingPeriodPostFilter.Apply(spans);
        if (PostFiltersConfig.RemoveTrailingSpaces)
            spans = TrailingSpacePostFilter.Apply(spans);
        spans = IgnoredTermsPostFilter.Apply(spans, Ignored);
        spans = IgnoredPatternsPostFilter.Apply(spans, IgnoredPatterns);
        return spans;
    }

    /// <summary>
    ///     Returns all whitespace-delimited n-grams of every length from <paramref name="length" /> down to 1,
    ///     each paired with its character <see cref="Position" /> in <paramref name="text" />.
    /// </summary>
    protected static List<(Position Position, string Ngram)> GetNgramsUpToLength(string text, int length)
    {
        var ngrams = new List<(Position, string)>();
        for (var n = length; n > 0; n--)
        {
            ngrams.AddRange(GetNgramsOfLength(text, n));
        }

        return ngrams;
    }

    /// <summary>
    ///     Returns the whitespace-delimited n-grams of exactly <paramref name="length" /> words, each paired
    ///     with its character <see cref="Position" /> in <paramref name="text" />.
    /// </summary>
    protected static List<(Position Position, string Ngram)> GetNgramsOfLength(string text, int length)
    {
        var ngrams = new List<(Position, string)>();
        var words = text.Split(' ');
        var lastLocation = 0;

        for (var i = 0; i <= words.Length - length; i++)
        {
            var ngram = string.Join(' ', words.Skip(i).Take(length));
            var newLocation = text.IndexOf(ngram, lastLocation, StringComparison.Ordinal);
            if (newLocation < 0) continue;
            lastLocation = newLocation;
            ngrams.Add((new Position(newLocation, newLocation + ngram.Length), ngram));
        }

        return ngrams;
    }
}