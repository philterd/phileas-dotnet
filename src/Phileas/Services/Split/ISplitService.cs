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

namespace Phileas.Services.Split;

/// <summary>Splits a document into pieces small enough to filter individually.</summary>
public interface ISplitService
{
    /// <summary>Splits <paramref name="input" /> into pieces.</summary>
    List<string> Split(string input);

    /// <summary>Returns the separator used to rejoin the pieces.</summary>
    string GetSeparator();

    /// <summary>
    ///     Splits <paramref name="input" /> and locates each piece in it, so spans detected in a piece
    ///     can be mapped back to offsets in the original text. Each piece after the first carries the
    ///     trailing <paramref name="overlap" /> characters of the previous one, so an entity straddling
    ///     a boundary is seen whole in the later piece.
    /// </summary>
    /// <param name="input">The text to split.</param>
    /// <param name="overlap">The number of characters each piece shares with the previous one.</param>
    /// <returns>
    ///     The located pieces, or <see langword="null" /> when they are not verbatim substrings of the
    ///     input. A caller given <see langword="null" /> must filter each piece on its own and cannot
    ///     report offsets into the input.
    /// </returns>
    IReadOnlyList<TextSplit>? SplitWithOverlap(string input, int overlap)
    {
        var pieces = Split(input);
        var splits = new List<TextSplit>(pieces.Count);
        var cursor = 0;

        foreach (var piece in pieces)
        {
            // Each piece must begin at the cursor, give or take the whitespace the splitter consumed.
            // Searching further ahead would risk matching identical text elsewhere and mislocating the
            // spans found in it.
            var start = cursor;
            while (start < input.Length && AbstractSplitService.IsTrimmable(input[start])) start++;

            if (string.CompareOrdinal(input, start, piece, 0, piece.Length) != 0)
                return null;

            // The first piece has no previous piece to share with.
            var from = splits.Count == 0 ? start : Math.Max(0, start - Math.Max(0, overlap));
            splits.Add(new TextSplit(input[from..(start + piece.Length)], from));

            cursor = start + piece.Length;
        }

        return splits;
    }
}
