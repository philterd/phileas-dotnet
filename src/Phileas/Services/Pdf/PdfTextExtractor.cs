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
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Phileas.Services.Pdf;

/// <summary>
///     Extracts positioned text from a PDF using PdfPig. Words are grouped into visual lines by their
///     baseline, and each character of a line keeps a reference to its source glyph so a detected span
///     can be mapped back to a bounding box on the page.
/// </summary>
public sealed class PdfTextExtractor : ITextExtractor
{
    /// <inheritdoc />
    public IReadOnlyList<PdfLine> GetLines(byte[] document)
    {
        var lines = new List<PdfLine>();

        using var pdf = PdfDocument.Open(document);
        foreach (var page in pdf.GetPages())
        {
            var words = page.GetWords().Where(w => w.Letters.Count > 0).ToList();
            foreach (var lineWords in GroupIntoLines(words))
                lines.Add(BuildLine(page.Number, lineWords));
        }

        return lines;
    }

    /// <summary>The baseline Y (PDF user space) of a word, used to cluster words into visual lines.</summary>
    private static double Baseline(Word word) => word.Letters[0].StartBaseLine.Y;

    /// <summary>
    ///     Groups words into visual lines by clustering on baseline. Words are processed top-to-bottom; a
    ///     word starts a new line when its baseline drops more than a tolerance below the current line's
    ///     baseline. Each line is then ordered left-to-right.
    /// </summary>
    private static List<List<Word>> GroupIntoLines(List<Word> words)
    {
        var ordered = words.OrderByDescending(Baseline).ToList();

        var lines = new List<List<Word>>();
        List<Word>? current = null;
        var currentBaseline = double.NaN;

        foreach (var word in ordered)
        {
            var baseline = Baseline(word);
            var tolerance = Math.Max(2.0, word.BoundingBox.Height * 0.4);

            if (current == null || Math.Abs(baseline - currentBaseline) > tolerance)
            {
                current = new List<Word>();
                lines.Add(current);
                currentBaseline = baseline;
            }

            current.Add(word);
        }

        foreach (var line in lines)
            line.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));

        return lines;
    }

    /// <summary>Builds a <see cref="PdfLine" /> from the words of a single visual line.</summary>
    private static PdfLine BuildLine(int pageNumber, List<Word> lineWords)
    {
        var text = new StringBuilder();
        var charBoxes = new List<CharBox?>();

        for (var w = 0; w < lineWords.Count; w++)
        {
            if (w > 0)
            {
                // Words are separated by a single space that has no glyph of its own.
                text.Append(' ');
                charBoxes.Add(null);
            }

            foreach (var letter in lineWords[w].Letters)
            {
                var value = letter.Value ?? string.Empty;
                text.Append(value);
                var box = letter.BoundingBox;
                var charBox = new CharBox(box.Left, box.Bottom, box.Right, box.Top);
                for (var c = 0; c < value.Length; c++)
                    charBoxes.Add(charBox);
            }
        }

        return new PdfLine(pageNumber, text.ToString(), charBoxes);
    }
}
