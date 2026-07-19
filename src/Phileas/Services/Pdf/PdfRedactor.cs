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

using System.Globalization;
using System.IO.Compression;
using PDFtoImage;
using Phileas.Model;
using SkiaSharp;
using UglyToad.PdfPig;
using BoundingBoxModel = Phileas.Policy.BoundingBox;
using PdfConfig = Phileas.Policy.Pdf;

namespace Phileas.Services.Pdf;

/// <summary>
///     Renders each PDF page to a raster image, burns redaction rectangles (and optional replacement
///     text) over the detected spans and graphical bounding boxes, and reassembles the pages into an
///     image-only PDF or a ZIP of per-page images. Because every page becomes an image, the output has
///     no recoverable text layer. Mirrors the Java <c>PdfRedactor</c>.
/// </summary>
public sealed class PdfRedactor
{
    /// <summary>
    ///     Redacts the document. Spans must already carry their page number and PDF-user-space coordinates.
    /// </summary>
    /// <param name="document">The source PDF bytes.</param>
    /// <param name="spans">The detected spans to redact.</param>
    /// <param name="pdf">The PDF redaction options.</param>
    /// <param name="boundingBoxes">Fixed graphical bounding boxes to redact.</param>
    /// <param name="outputMimeType">The desired output format.</param>
    /// <returns>The redacted document bytes (a PDF or a ZIP of images).</returns>
    public byte[] Process(byte[] document, IList<Span> spans, PdfConfig pdf,
        IList<BoundingBoxModel> boundingBoxes, MimeType outputMimeType)
    {
        // Page dimensions (PDF user-space points) are needed to map coordinates and size output pages.
        var pageSizes = new List<(double Width, double Height)>();
        using (var pdfDocument = PdfDocument.Open(document))
        {
            foreach (var page in pdfDocument.GetPages())
                pageSizes.Add((page.Width, page.Height));
        }

        var bitmaps = new List<SKBitmap>();
        try
        {
            for (var pageNumber = 1; pageNumber <= pageSizes.Count; pageNumber++)
            {
                var (widthPts, heightPts) = pageSizes[pageNumber - 1];

                var bitmap = Conversion.ToImage(document, page: pageNumber - 1,
                    options: new RenderOptions(Dpi: pdf.Dpi));

                var scaleX = bitmap.Width / widthPts;
                var scaleY = bitmap.Height / heightPts;

                using (var canvas = new SKCanvas(bitmap))
                {
                    foreach (var span in spans.Where(s => s.PageNumber == pageNumber))
                    {
                        var rect = ToPixelRect(span.LowerLeftX, span.LowerLeftY, span.UpperRightX,
                            span.UpperRightY, heightPts, scaleX, scaleY);
                        // A span with no located box (e.g. one detected in an annotation or form field, whose
                        // text isn't rendered into the image) has nothing to burn in — skip it.
                        if (rect.Width <= 0 || rect.Height <= 0)
                            continue;
                        // The bar uses the redacting strategy's color when set, else the policy-wide PDF color,
                        // else black. A set-but-unrecognized color renders black (never left un-redacted).
                        using var redactionPaint = new SKPaint
                            { Color = ParseColor(span.Color ?? pdf.RedactionColor, SKColors.Black), Style = SKPaintStyle.Fill };
                        canvas.DrawRect(rect, redactionPaint);

                        if (pdf.ShowReplacement && !string.IsNullOrEmpty(span.Replacement))
                            DrawReplacement(canvas, span.Replacement, rect, pdf, scaleY);
                    }

                    foreach (var box in boundingBoxes.Where(b => BoxAppliesToPage(b, pageNumber)))
                    {
                        var rect = ToPixelRect(box.X, box.Y, box.X + box.W, box.Y + box.H,
                            heightPts, scaleX, scaleY);
                        using var boxPaint = new SKPaint
                            { Color = ParseColor(box.Color ?? pdf.RedactionColor, SKColors.Black), Style = SKPaintStyle.Fill };
                        canvas.DrawRect(rect, boxPaint);
                    }
                }

                bitmaps.Add(bitmap);
            }

            return outputMimeType == MimeType.ImageJpeg
                ? BuildImageArchive(bitmaps, pdf)
                : BuildPdf(bitmaps, pageSizes, pdf);
        }
        finally
        {
            foreach (var bitmap in bitmaps)
                bitmap.Dispose();
        }
    }

    /// <summary>
    ///     Whether a bounding box applies to a given 1-based page. A box's <see cref="BoundingBoxModel.Page" />
    ///     is normally the exact page it covers, but two values are open-ended so a single box can span pages
    ///     without knowing the page count up front: <c>0</c> covers <b>every</b> page, and a negative value
    ///     <c>-N</c> covers page <c>N</c> through the last page (so <c>-2</c> is "all but the first page").
    /// </summary>
    internal static bool BoxAppliesToPage(BoundingBoxModel box, int pageNumber) => box.Page switch
    {
        0 => true,                     // all pages
        < 0 => pageNumber >= -box.Page, // from page N to the last page
        _ => box.Page == pageNumber     // an exact page
    };

    /// <summary>Maps a PDF-user-space rectangle (bottom-left origin) to a pixel rectangle on the rasterized page (top-left origin).</summary>
    private static SKRect ToPixelRect(double lowerLeftX, double lowerLeftY, double upperRightX,
        double upperRightY, double pageHeightPts, double scaleX, double scaleY)
    {
        var left = (float)(lowerLeftX * scaleX);
        var right = (float)(upperRightX * scaleX);
        // PDF Y increases upward; image Y increases downward, so flip about the page height.
        var top = (float)((pageHeightPts - upperRightY) * scaleY);
        var bottom = (float)((pageHeightPts - lowerLeftY) * scaleY);
        return new SKRect(left, top, right, bottom);
    }

    private byte[] BuildPdf(List<SKBitmap> bitmaps, List<(double Width, double Height)> pageSizes, PdfConfig pdf)
    {
        using var stream = new MemoryStream();
        var metadata = new SKDocumentPdfMetadata
        {
            RasterDpi = pdf.Dpi,
            EncodingQuality = ToEncodingQuality(pdf.CompressionQuality)
        };

        using (var document = SKDocument.CreatePdf(stream, metadata))
        {
            for (var i = 0; i < bitmaps.Count; i++)
            {
                var (widthPts, heightPts) = pageSizes[i];
                var outWidth = (float)(widthPts * pdf.Scale);
                var outHeight = (float)(heightPts * pdf.Scale);

                var canvas = document.BeginPage(outWidth, outHeight);
                canvas.DrawBitmap(bitmaps[i], new SKRect(0, 0, outWidth, outHeight));
                document.EndPage();
            }

            document.Close();
        }

        return stream.ToArray();
    }

    private static byte[] BuildImageArchive(List<SKBitmap> bitmaps, PdfConfig pdf)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var i = 0; i < bitmaps.Count; i++)
            {
                var entry = archive.CreateEntry($"page-{i}.jpeg", CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                using var data = bitmaps[i].Encode(SKEncodedImageFormat.Jpeg, ToEncodingQuality(pdf.CompressionQuality));
                data.SaveTo(entryStream);
            }
        }

        return stream.ToArray();
    }

    private static void DrawReplacement(SKCanvas canvas, string text, SKRect rect, PdfConfig pdf, double scaleY)
    {
        var typeface = SKTypeface.FromFamilyName(MapFont(pdf.ReplacementFont)) ?? SKTypeface.Default;

        var maxSize = (float)(pdf.ReplacementMaxFontSize * scaleY);
        var size = Math.Min(maxSize, rect.Height * 0.8f);
        if (size < 1f)
            return;

        using var font = new SKFont(typeface, size);

        // Shrink the font until the text fits the rectangle's width.
        var width = font.MeasureText(text);
        while (width > rect.Width && font.Size > 1f)
        {
            font.Size *= 0.9f;
            width = font.MeasureText(text);
        }

        using var textPaint = new SKPaint
            { Color = ParseColor(pdf.ReplacementFontColor ?? "white", SKColors.White), IsAntialias = true };

        var metrics = font.Metrics;
        var x = rect.MidX - width / 2f;
        var y = rect.MidY - (metrics.Ascent + metrics.Descent) / 2f;
        canvas.DrawText(text, x, y, font, textPaint);
    }

    private static int ToEncodingQuality(float compressionQuality)
    {
        return Math.Clamp((int)(compressionQuality * 100), 1, 100);
    }

    /// <summary>
    ///     Resolves a policy color string to an <see cref="SKColor" />. Accepts the named colors black, white, red,
    ///     orange, yellow, green, blue, and gray (grey), or a 6-digit hex string matching <c>^#[0-9A-Fa-f]{6}$</c>. A
    ///     <see langword="null" />, empty, unrecognized, or malformed value resolves to <paramref name="fallback" />.
    /// </summary>
    internal static SKColor ParseColor(string? name, SKColor fallback)
    {
        var value = name?.Trim();
        if (string.IsNullOrEmpty(value))
            return fallback;

        switch (value.ToLowerInvariant())
        {
            case "black": return SKColors.Black;
            case "white": return SKColors.White;
            case "red": return SKColors.Red;
            case "orange": return SKColors.Orange;
            case "yellow": return SKColors.Yellow;
            case "green": return SKColors.Green;
            case "blue": return SKColors.Blue;
            case "gray":
            case "grey": return SKColors.Gray;
        }

        // 6-digit hex, e.g. "#ff8800". Anything else (short hex, bad digits, unknown name) falls back.
        if (value.Length == 7 && value[0] == '#'
            && byte.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            && byte.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            && byte.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            return new SKColor(r, g, b);

        return fallback;
    }

    private static string MapFont(string? font)
    {
        return (font?.Trim().ToLowerInvariant()) switch
        {
            "times" => "Times New Roman",
            "courier" => "Courier New",
            _ => "Helvetica"
        };
    }
}
