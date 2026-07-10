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

namespace Phileas.Services.Office;

/// <summary>A text replacement range used when applying redaction spans to a paragraph or cell.</summary>
internal readonly record struct ReplacementRange(int Start, int End, string Replacement);

/// <summary>Resolves overlapping replacement ranges before they are applied to text.</summary>
internal static class OfficeSpanMath
{
    /// <summary>
    ///     Sorts ranges by start and drops any that overlap one already kept (highest priority is
    ///     earliest start, then longest). Returns them ordered by start ascending.
    /// </summary>
    public static List<ReplacementRange> ResolveNonOverlapping(IEnumerable<ReplacementRange> ranges)
    {
        var result = new List<ReplacementRange>();
        int lastEnd = -1;
        foreach (ReplacementRange r in ranges.Where(r => r.End > r.Start).OrderBy(r => r.Start).ThenByDescending(r => r.End))
        {
            if (r.Start >= lastEnd)
            {
                result.Add(r);
                lastEnd = r.End;
            }
        }
        return result;
    }
}
