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
using Phileas.Policy;
using RegexOptions = System.Text.RegularExpressions.RegexOptions;

namespace Phileas.Filters.PostFilters;

/// <summary>
///     Post-filter that removes spans whose entity text matches any of the filter's configured ignored-pattern
///     regular expressions.
/// </summary>
public static class IgnoredPatternsPostFilter
{
    /// <summary>
    ///     Returns only those spans whose <see cref="Phileas.Model.Span.Text" /> does not match any pattern in
    ///     <paramref name="ignoredPatterns" />.
    /// </summary>
    /// <param name="spans">The list of spans to post-filter.</param>
    /// <param name="ignoredPatterns">The list of regex-based patterns for text that should be excluded.</param>
    /// <returns>A filtered list containing only spans whose text does not match any ignored pattern.</returns>
    public static IList<Span> Apply(IList<Span> spans, IList<IgnoredPattern> ignoredPatterns)
    {
        if (ignoredPatterns == null || ignoredPatterns.Count == 0)
            return spans;

        return spans.Where(span =>
        {
            foreach (var pattern in ignoredPatterns)
            {
                if (pattern.Pattern == null) continue;
                var options = pattern.CaseSensitive
                    ? RegexOptions.None
                    : RegexOptions.IgnoreCase;
                if (Regex.IsMatch(span.Text, pattern.Pattern, options))
                    return false;
            }

            return true;
        }).ToList();
    }
}