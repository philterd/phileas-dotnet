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
using Phileas.Policy;
using Phileas.Services.Pdf;
using SkiaSharp;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;
using PdfConfig = Phileas.Policy.Pdf;

namespace Phileas.Tests.Pdfs;

/// <summary>
///     A span's redaction box covers every rendered pixel of its glyphs. The box was drawn exactly at the
///     glyph boxes PdfPig reports, and rendered glyphs extend past them, so the edges of round characters,
///     descenders and the last character of a span stayed visible around the box. Each page holds only the
///     text being redacted, so after redaction any non-white pixel left on it is part of a redacted glyph.
///     See philterd/phileas-dotnet#175.
/// </summary>
[Collection("Pdf")]
public class PdfRedactionCoverageTests
{
    private const string Text = "06/02/2019 O0698 gjpqy Ogjpqy|[]Q,;$@ 44556677889900";

    public static TheoryData<string, double, int> Cases()
    {
        var data = new TheoryData<string, double, int>();
        foreach (var font in new[] { "Helvetica", "HelveticaBold", "TimesRoman", "Courier" })
            foreach (var size in new[] { 6.0, 9.0, 10.0, 10.5, 24.0 })
                // 150 is the default dpi and 200 the one Philter Desktop uses.
                foreach (var dpi in new[] { 72, 150, 200, 400 })
                    data.Add(font, size, dpi);
        return data;
    }

    private static byte[] TextPdf(string font, double size)
    {
        var builder = new PdfDocumentBuilder();
        var added = builder.AddStandard14Font(Enum.Parse<Standard14Font>(font));
        builder.AddPage(612, 792).AddText(Text, size, new PdfPoint(72, 400), added);
        return builder.Build();
    }

    /// <summary>One located span per word, or per character, built the way PdfFilterService locates them.</summary>
    private static List<Span> Spans(byte[] pdf, bool perCharacter)
    {
        var line = Assert.Single(new PdfTextExtractor().GetLines(pdf));
        var ranges = new List<(int Start, int End)>();
        var start = -1;
        for (var i = 0; i <= line.Text.Length; i++)
        {
            var located = i < line.Text.Length && line.CharBoxes[i] != null;
            if (perCharacter && located)
                ranges.Add((i, i + 1));
            else if (located && start < 0)
                start = i;
            else if (!located && start >= 0)
            {
                ranges.Add((start, i));
                start = -1;
            }
        }

        return ranges.Select(range =>
        {
            var boxes = Enumerable.Range(range.Start, range.End - range.Start).Select(i => line.CharBoxes[i]!.Value).ToList();
            return new Span
            {
                PageNumber = 1,
                LowerLeftX = boxes.Min(b => b.Left),
                LowerLeftY = boxes.Min(b => b.Bottom),
                UpperRightX = boxes.Max(b => b.Right),
                UpperRightY = boxes.Max(b => b.Top)
            };
        }).ToList();
    }

    private static List<(int X, int Y)> GlyphPixelsLeft(string font, double size, int dpi, bool perCharacter)
    {
        var pdf = TextPdf(font, size);
        using var page = PdfRedactor.RenderPage(pdf, 1, (612, 792), Spans(pdf, perCharacter),
            new List<BoundingBox>(), new PdfConfig { Dpi = dpi, RedactionColor = "red" });

        var left = new List<(int, int)>();
        for (var y = 0; y < page.Height; y++)
            for (var x = 0; x < page.Width; x++)
            {
                // The boxes are red, so a glyph pixel, black or anti-aliased grey, is neither red nor white.
                var pixel = page.GetPixel(x, y);
                if (pixel != SKColors.Red && pixel != SKColors.White)
                    left.Add((x, y));
            }

        return left;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EachWordsBoxCoversItsRenderedGlyphs(string font, double size, int dpi)
    {
        var left = GlyphPixelsLeft(font, size, dpi, perCharacter: false);

        Assert.True(left.Count == 0, $"{left.Count} glyph pixels left outside the boxes, first at {left.FirstOrDefault()}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EachCharactersBoxCoversItsRenderedGlyph(string font, double size, int dpi)
    {
        // Each character is its own span, so every character is both the first and the last of one.
        var left = GlyphPixelsLeft(font, size, dpi, perCharacter: true);

        Assert.True(left.Count == 0, $"{left.Count} glyph pixels left outside the boxes, first at {left.FirstOrDefault()}");
    }

    [Fact]
    public void ASpansBoxIsPaddedAndRoundedOutwardToWholePixels()
    {
        var padded = PdfRedactor.PadSpanRect(new SKRect(100.4f, 50.6f, 200.2f, 60.6f));

        // Height 10, so 2.5 pixels of padding plus 1 for anti-aliasing on each side.
        Assert.Equal(new SKRect(96, 47, 204, 65), padded);
    }
}
