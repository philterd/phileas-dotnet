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
///     Post-filter that removes trailing newline characters (<c>\n</c>, <c>\r</c>) from the end of each matched
///     entity's text, adjusting the character end index accordingly. Spans that become empty after trimming are
///     discarded.
/// </summary>
public static class TrailingNewLinesPostFilter
{
    /// <summary>
    ///     Removes trailing newline characters (<c>\n</c>, <c>\r</c>) from each span's
    ///     <see cref="Phileas.Model.Span.Text" />, updates the <see cref="Phileas.Model.Span.CharacterEnd" /> offset
    ///     to match, and discards any spans that become empty.
    /// </summary>
    /// <param name="spans">The list of spans to post-filter.</param>
    /// <returns>A new list of spans with trailing newlines removed.</returns>
    public static IList<Span> Apply(IList<Span> spans)
    {
        var result = new List<Span>();
        foreach (var span in spans)
        {
            var text = span.Text;
            if (!string.IsNullOrEmpty(text))
            {
                var trimCount = 0;
                while (trimCount < text.Length &&
                       (text[text.Length - 1 - trimCount] == '\n' || text[text.Length - 1 - trimCount] == '\r'))
                    trimCount++;

                if (trimCount > 0)
                {
                    span.Text = text.Substring(0, text.Length - trimCount);
                    span.CharacterEnd -= trimCount;
                }
            }

            if (span.CharacterEnd > span.CharacterStart)
                result.Add(span);
        }

        return result;
    }
}