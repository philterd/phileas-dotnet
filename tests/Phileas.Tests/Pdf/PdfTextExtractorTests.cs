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

using Phileas.Services.Pdf;
using UglyToad.PdfPig;
using Xunit;

namespace Phileas.Tests.Pdfs;

/// <summary>
///     Verifies that <see cref="PdfTextExtractor" /> maps PdfPig glyphs into the engine's
///     library-independent <see cref="CharBox" /> model correctly: one box slot per character (the
///     invariant the redactor's span-location relies on), positioned glyph boxes for visible
///     characters, and null boxes for the synthesized word separators.
/// </summary>
[Collection("Pdf")]
public class PdfTextExtractorTests
{
    private static byte[] SamplePdf() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Resources", "sample.pdf"));

    [Fact]
    public void GetLines_HasOneBoxPerCharacter_WithPositionedGlyphsAndNullSeparators()
    {
        IReadOnlyList<PdfLine> lines = new PdfTextExtractor().GetLines(SamplePdf());
        Assert.NotEmpty(lines);

        foreach (PdfLine line in lines)
        {
            // The core invariant: every character has a corresponding box slot (TryLocate indexes
            // CharBoxes by character offset, so a mismatch would mislocate or crash redaction).
            Assert.Equal(line.Text.Length, line.CharBoxes.Count);

            for (var i = 0; i < line.Text.Length; i++)
            {
                CharBox? box = line.CharBoxes[i];
                if (line.Text[i] == ' ')
                {
                    // Word separators are synthesized and have no glyph.
                    Assert.Null(box);
                }
                else
                {
                    Assert.True(box is not null, $"visible char '{line.Text[i]}' should have a box");
                    CharBox b = box!.Value;
                    Assert.True(b.Right > b.Left, "box should have positive width");
                    Assert.True(b.Top > b.Bottom, "box should have positive height");
                }
            }
        }
    }

    [Fact]
    public void GetLines_BoxesAreOnTheirPage_AndWithinPageBounds()
    {
        byte[] pdf = SamplePdf();

        var pageSizes = new Dictionary<int, (double Width, double Height)>();
        using (PdfDocument document = PdfDocument.Open(pdf))
        {
            foreach (var page in document.GetPages())
            {
                pageSizes[page.Number] = (page.Width, page.Height);
            }
        }

        IReadOnlyList<PdfLine> lines = new PdfTextExtractor().GetLines(pdf);

        foreach (PdfLine line in lines)
        {
            Assert.True(pageSizes.ContainsKey(line.PageNumber));
            (double width, double height) = pageSizes[line.PageNumber];

            foreach (CharBox? box in line.CharBoxes)
            {
                if (box is not { } b)
                {
                    continue;
                }
                // Allow a small tolerance for glyphs that overhang the page edge slightly.
                Assert.InRange(b.Left, -2, width + 2);
                Assert.InRange(b.Right, -2, width + 2);
                Assert.InRange(b.Bottom, -2, height + 2);
                Assert.InRange(b.Top, -2, height + 2);
            }
        }
    }
}
