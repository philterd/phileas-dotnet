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
using SkiaSharp;
using UglyToad.PdfPig;
using Xunit;

namespace Phileas.Tests.Pdfs;

/// <summary>
///     De-risking smoke tests that exercise the native PDF stack end-to-end (PdfPig text extraction,
///     PDFium rasterization, and SkiaSharp drawing / encoding / PDF assembly) before the redaction
///     pipeline is built on top of it.
/// </summary>
[Collection("Pdf")]
public class PdfStackSmokeTests
{
    private static byte[] SamplePdf() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Resources", "sample.pdf"));

    [Fact]
    public void PdfPig_ExtractsTextWithGlyphCoordinates()
    {
        using var document = PdfDocument.Open(SamplePdf());

        Assert.True(document.NumberOfPages > 0);

        var page = document.GetPage(1);
        Assert.NotEmpty(page.Letters);

        // Every glyph carries a positioned bounding box in PDF user space.
        var letter = page.Letters.First(l => !string.IsNullOrWhiteSpace(l.Value));
        Assert.True(letter.BoundingBox.Width > 0);
        Assert.True(page.Height > 0 && page.Width > 0);
    }

    [Fact]
    public void Pdfium_RasterizesAPageToABitmap()
    {
        using var bitmap = PDFtoImage.Conversion.ToImage(SamplePdf());

        Assert.NotNull(bitmap);
        Assert.True(bitmap.Width > 0);
        Assert.True(bitmap.Height > 0);
    }

    [Fact]
    public void Skia_DrawsRectangle_EncodesJpeg_AndAssemblesAnImagePdf()
    {
        using var bitmap = PDFtoImage.Conversion.ToImage(SamplePdf());

        // Draw a filled redaction rectangle on the rasterized page.
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill })
        {
            canvas.DrawRect(SKRect.Create(10, 10, 100, 20), paint);
        }

        // Encode to JPEG.
        using var jpeg = bitmap.Encode(SKEncodedImageFormat.Jpeg, 90);
        Assert.NotNull(jpeg);
        Assert.True(jpeg.ToArray().Length > 0);

        // Assemble an image-only PDF (the redacted output format).
        using var stream = new MemoryStream();
        using (var pdf = SKDocument.CreatePdf(stream))
        {
            var canvas = pdf.BeginPage(bitmap.Width, bitmap.Height);
            canvas.DrawBitmap(bitmap, 0, 0);
            pdf.EndPage();
            pdf.Close();
        }

        var pdfBytes = stream.ToArray();
        Assert.True(pdfBytes.Length > 0);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdfBytes, 0, 4));
    }
}
