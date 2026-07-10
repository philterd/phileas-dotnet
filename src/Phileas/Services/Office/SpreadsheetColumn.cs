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

namespace Phileas.Services.Office
{
    /// <summary>
    /// Describes one column of a tabular document (spreadsheet or CSV) for the "redact entire column"
    /// picker: its 1-based <see cref="Index"/>, its spreadsheet <see cref="Letter"/> (A, B, C, …), and
    /// the <see cref="Header"/> text from the first row (may be empty).
    /// </summary>
    public sealed record SpreadsheetColumn(int Index, string Letter, string Header)
    {
        /// <summary>A friendly label like "A — Name" or just "A" when there's no header.</summary>
        public string Label => string.IsNullOrWhiteSpace(Header) ? Letter : $"{Letter} — {Header}";

        /// <summary>The spreadsheet letter for a 1-based column index (1 → A, 27 → AA).</summary>
        public static string IndexToLetter(int index)
        {
            string letter = string.Empty;
            while (index > 0)
            {
                int remainder = (index - 1) % 26;
                letter = (char)('A' + remainder) + letter;
                index = (index - 1) / 26;
            }
            return letter;
        }

        /// <summary>The 1-based column index from a cell reference like "C2" (→ 3); 0 if unparseable.</summary>
        public static int LetterToIndex(string? cellReference)
        {
            if (string.IsNullOrEmpty(cellReference))
            {
                return 0;
            }
            int index = 0;
            foreach (char c in cellReference)
            {
                if (char.IsLetter(c))
                {
                    index = index * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
                }
                else
                {
                    break;
                }
            }
            return index;
        }
    }
}
