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

using System.Text;

namespace Phileas.Services.Split;

/// <summary>
///     Splits a document into lines no wider than a fixed width, wrapping at spaces. The wrapping
///     mirrors Apache Commons Text <c>WordUtils.wrap</c> (long words are not broken).
/// </summary>
public class LineWidthSplitService : AbstractSplitService, ISplitService
{
    private readonly int _lineWidth;

    /// <summary>Creates a splitter producing lines of at most <paramref name="lineWidth" /> characters.</summary>
    public LineWidthSplitService(int lineWidth)
    {
        _lineWidth = lineWidth;
    }

    /// <inheritdoc />
    public List<string> Split(string input)
    {
        return Clean(Wrap(input, _lineWidth).Split('\n'));
    }

    /// <inheritdoc />
    public string GetSeparator() => " ";

    /// <summary>Word-wraps <paramref name="str" /> at spaces; long words are left unbroken.</summary>
    internal static string Wrap(string str, int wrapLength)
    {
        var sb = new StringBuilder();
        var offset = 0;
        var length = str.Length;

        while (offset < length)
        {
            // Skip a leading space at the wrap position.
            if (str[offset] == ' ')
            {
                offset++;
                continue;
            }

            // The remainder fits on one line.
            if (length - offset <= wrapLength) break;

            // Break at the last space within the window, if any.
            var windowEnd = Math.Min(offset + wrapLength + 1, length);
            var spaceToWrapAt = str.LastIndexOf(' ', windowEnd - 1, windowEnd - offset);

            if (spaceToWrapAt > offset)
            {
                sb.Append(str, offset, spaceToWrapAt - offset).Append('\n');
                offset = spaceToWrapAt + 1;
            }
            else
            {
                // A word longer than the wrap length: do not break it; wrap at the next space after it.
                var next = str.IndexOf(' ', offset + wrapLength);
                if (next >= 0)
                {
                    sb.Append(str, offset, next - offset).Append('\n');
                    offset = next + 1;
                }
                else
                {
                    sb.Append(str, offset, length - offset);
                    offset = length;
                }
            }
        }

        sb.Append(str, offset, length - offset);
        return sb.ToString();
    }
}
