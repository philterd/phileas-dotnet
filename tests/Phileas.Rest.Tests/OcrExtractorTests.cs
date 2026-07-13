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

using Phileas.Rest;
using Phileas.Rest.Ocr;
using Phileas.Services.Pdf;
using SkiaSharp;
using Tesseract;
using Xunit;

namespace Phileas.Rest.Tests;

/// <summary>
///     Exercises <see cref="TesseractTextExtractor" /> against a generated image-only PDF (no text layer), which
///     is the scanned-document case OCR exists for. Skips when the native Tesseract library / tessdata aren't
///     present on the host — the same graceful-skip approach as the Docker-gated API tests.
///     <para>
///         The assertion is at the extractor level on purpose: PDF redaction output is rasterized (image-only),
///         so there is no output text layer to read back — the meaningful, deterministic check is that OCR
///         recovers the page text and maps it to on-page PDF coordinates.
///     </para>
/// </summary>
public sealed class OcrExtractorTests
{
    private const int PageWidthPts = 612;   // US Letter at 72 dpi
    private const int PageHeightPts = 792;

    [SkippableFact]
    public void Ocr_ExtractsText_AndMapsToPageCoordinates()
    {
        var tessDataPath = TessDataPath();
        Skip.IfNot(TesseractAvailable(tessDataPath), "Native Tesseract library or tessdata not available.");

        var pdf = BuildImageOnlyPdf("INVOICE 12345");

        var extractor = new TesseractTextExtractor(new OcrOptions
        {
            Mode = OcrMode.Always,
            Language = "eng",
            TessDataPath = tessDataPath,
            Dpi = 200
        });

        var lines = extractor.GetLines(pdf);

        // OCR recovered the page text.
        var text = string.Join(" ", lines.Select(l => l.Text));
        Assert.Contains("INVOICE", text, StringComparison.OrdinalIgnoreCase);
        Assert.All(lines, l => Assert.Equal(1, l.PageNumber));

        // The pixel→PDF-point conversion produced boxes that fall within the page bounds.
        var boxes = lines.SelectMany(l => l.CharBoxes).OfType<CharBox>().ToList();
        Assert.NotEmpty(boxes);
        Assert.All(boxes, b =>
        {
            Assert.InRange(b.Left, 0, PageWidthPts);
            Assert.InRange(b.Right, 0, PageWidthPts);
            Assert.InRange(b.Bottom, 0, PageHeightPts);
            Assert.InRange(b.Top, 0, PageHeightPts);
            Assert.True(b.Right >= b.Left && b.Top >= b.Bottom);
        });
    }

    private static string TessDataPath() =>
        Environment.GetEnvironmentVariable("Phileas__Ocr__TessDataPath")
        ?? Environment.GetEnvironmentVariable("TESSDATA_PREFIX")
        ?? "/usr/share/tesseract-ocr/5/tessdata";

    private static bool TesseractAvailable(string tessDataPath)
    {
        try
        {
            using var engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Builds a single-page PDF whose only content is a rasterized image of the given text — i.e. no text
    ///     layer, so the text is recoverable only via OCR (mimicking a scanned document).
    /// </summary>
    private static byte[] BuildImageOnlyPdf(string text)
    {
        using var picture = new SKBitmap(new SKImageInfo(PageWidthPts * 2, PageHeightPts * 2));
        using (var canvas = new SKCanvas(picture))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            using var font = new SKFont(SKTypeface.Default, 80);
            canvas.DrawText(text, 120, 400, SKTextAlign.Left, font, paint);
        }

        using var stream = new MemoryStream();
        using (var document = SKDocument.CreatePdf(stream))
        {
            var canvas = document.BeginPage(PageWidthPts, PageHeightPts);
            canvas.DrawBitmap(picture, new SKRect(0, 0, PageWidthPts, PageHeightPts));
            document.EndPage();
            document.Close();
        }

        return stream.ToArray();
    }
}
