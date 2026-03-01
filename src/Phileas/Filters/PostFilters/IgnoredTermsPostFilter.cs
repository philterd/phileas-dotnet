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

namespace Phileas.Filters.PostFilters;

/// <summary>
///     Post-filter that removes spans whose entity text appears in the filter's configured ignored-terms set.
/// </summary>
public static class IgnoredTermsPostFilter
{
    /// <summary>
    ///     Returns only those spans whose <see cref="Phileas.Model.Span.Text" /> is not present in
    ///     <paramref name="ignored" />.
    /// </summary>
    /// <param name="spans">The list of spans to post-filter.</param>
    /// <param name="ignored">The set of exact token values that should be excluded from results.</param>
    /// <returns>A filtered list containing only spans whose text is not in the ignored set.</returns>
    public static IList<Span> Apply(IList<Span> spans, ISet<string> ignored)
    {
        if (ignored == null || ignored.Count == 0)
            return spans;

        return spans.Where(span => !ignored.Contains(span.Text)).ToList();
    }
}