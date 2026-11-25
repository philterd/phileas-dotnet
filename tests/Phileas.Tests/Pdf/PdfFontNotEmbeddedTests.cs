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
using Phileas.Model;
using Phileas.Policy.Filters;
using Phileas.Services.Pdf;
using SkiaSharp;
using Xunit;
using PdfConfig = Phileas.Policy.Pdf;
using PhileasPolicy = Phileas.Policy.Policy;
using PolicyIdentifiers = Phileas.Policy.Identifiers;

namespace Phileas.Tests.Pdfs;

/// <summary>
///     Text drawn in a font that isn't embedded in the PDF, as the values of a filled and flattened form
///     usually are, is redacted. PdfPig measures a glyph's height from the font program, so without one every
///     letter's box had no height, and the redactor skipped the span: the value was detected but stayed
///     readable in the output.
/// </summary>
[Collection("Pdf")]
public class PdfFontNotEmbeddedTests
{
    private const string Ssn = "123-45-6789";
    private const double FontSize = 12;
    private const double Baseline = 400;

    // The font the IRS forms' fields are filled in, which they don't embed.
    private const string Type1 = "/Subtype /Type1 /BaseFont /HelveticaLTStd-Bold";

    public static TheoryData<string> Fonts() => new()
    {
        Type1,
        // PdfPig measures a TrueType font that isn't embedded from its descriptor, so this one was covered before.
        "/Subtype /TrueType /BaseFont /Arial"
    };

    /// <summary>A one-page PDF holding only <see cref="Ssn" />, in a font with metrics but no font program.</summary>
    private static byte[] PdfInFontNotEmbedded(string font)
    {
        var content = $"BT /F1 {FontSize} Tf 72 {Baseline} Td ({Ssn}) Tj ET";
        var widths = string.Join(' ', Enumerable.Repeat("556", 95));
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            $"<< /Type /Font {font} /FirstChar 32 /LastChar 126 /Widths [{widths}] /Encoding /WinAnsiEncoding /FontDescriptor 6 0 R >>",
            "<< /Type /FontDescriptor /FontName /NotEmbedded /Flags 32 /FontBBox [-170 -228 1003 962] /ItalicAngle 0 /Ascent 962 /Descent -228 /CapHeight 718 /StemV 140 >>"
        };
        using var stream = new MemoryStream();
        void W(string s) => stream.Write(Encoding.ASCII.GetBytes(s));
        W("%PDF-1.7\n");
        var offsets = new long[objects.Length + 1];
        for (var i = 0; i < objects.Length; i++)
        {
            offsets[i + 1] = stream.Position;
            W($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = stream.Position;
        W($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        for (var i = 1; i <= objects.Length; i++)
            W($"{offsets[i]:D10} 00000 n \n");
        W($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return stream.ToArray();
    }

    private static PhileasPolicy SsnPolicy() => new()
    {
        Name = "ssn",
        Identifiers = new PolicyIdentifiers { Ssn = new Ssn() }
    };

    [Fact]
    public void EachLetterHasABoxFromBelowTheBaselineToAFontSizeAboveIt()
    {
        var line = Assert.Single(new PdfTextExtractor().GetLines(PdfInFontNotEmbedded(Type1)));

        Assert.Equal(Ssn, line.Text);
        foreach (var box in line.CharBoxes)
        {
            Assert.NotNull(box);
            Assert.Equal(Baseline - FontSize / 4, box!.Value.Bottom, 3);
            Assert.Equal(Baseline + FontSize, box.Value.Top, 3);
            Assert.True(box.Value.Right > box.Value.Left);
        }
    }

    [Theory]
    [MemberData(nameof(Fonts))]
    public void TheDetectedValueIsCovered(string font)
    {
        var pdf = PdfInFontNotEmbedded(font);
        var result = new PdfFilterService().Filter(SsnPolicy(), "default", pdf, MimeType.ApplicationPdf);

        var span = Assert.Single(result.Spans);
        Assert.Equal(Ssn, span.Text);
        Assert.True(span.UpperRightY > span.LowerLeftY);

        // The page holds only the SSN, so with red boxes any pixel that is neither red nor white is part of a
        // glyph left readable.
        using var page = PdfRedactor.RenderPage(pdf, 1, (612, 792), result.Spans, new List<Phileas.Policy.BoundingBox>(),
            new PdfConfig { Dpi = 150, RedactionColor = "red" });
        var left = 0;
        for (var y = 0; y < page.Height; y++)
            for (var x = 0; x < page.Width; x++)
            {
                var pixel = page.GetPixel(x, y);
                if (pixel != SKColors.Red && pixel != SKColors.White)
                    left++;
            }

        Assert.Equal(0, left);
    }
}
