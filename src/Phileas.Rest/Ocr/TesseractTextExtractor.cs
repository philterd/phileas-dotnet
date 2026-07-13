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
using PDFtoImage;
using Phileas.Services.Pdf;
using SkiaSharp;
using Tesseract;
using UglyToad.PdfPig;

namespace Phileas.Rest.Ocr;

/// <summary>
///     An OCR-backed <see cref="ITextExtractor" />. Each PDF page is rasterized (PDFium via PDFtoImage) and run
///     through Tesseract; the recognized words — grouped into lines — are returned as <see cref="PdfLine" />s
///     with per-character boxes in PDF user-space points (bottom-left origin), so a detected span maps back to a
///     rectangle on the page exactly as it does for the text-layer extractor.
///     <para>
///         This makes scanned / image-only PDFs (which have no text layer) redactable. Requires the native
///         Tesseract library and the <c>tessdata</c> language files to be present (both installed in the Docker
///         image); the tessdata directory and language are configured via <see cref="OcrOptions" />.
///     </para>
/// </summary>
public sealed class TesseractTextExtractor : ITextExtractor
{
    private readonly OcrOptions _options;

    public TesseractTextExtractor(OcrOptions options) => _options = options;

    /// <inheritdoc />
    public IReadOnlyList<PdfLine> GetLines(byte[] document) => Extract(document, onlyPages: null);

    /// <summary>
    ///     OCRs the given document, optionally restricted to <paramref name="onlyPages" /> (1-based page numbers).
    ///     Used by the fallback extractor to OCR only the pages that had no text layer.
    /// </summary>
    public IReadOnlyList<PdfLine> Extract(byte[] document, ISet<int>? onlyPages)
    {
        var pageSizes = new List<(int Number, double Width, double Height)>();
        using (var pdf = PdfDocument.Open(document))
        {
            foreach (var page in pdf.GetPages())
                pageSizes.Add((page.Number, page.Width, page.Height));
        }

        var lines = new List<PdfLine>();

        // One engine per call keeps the extractor safe to share across concurrent requests (TesseractEngine is
        // not thread-safe). Engine construction is cheap relative to the OCR of a page.
        using var engine = new TesseractEngine(_options.TessDataPath, _options.Language, EngineMode.Default);

        foreach (var (number, widthPts, heightPts) in pageSizes)
        {
            if (onlyPages != null && !onlyPages.Contains(number))
                continue;

            using var bitmap = Conversion.ToImage(document, page: number - 1, options: new RenderOptions(Dpi: _options.Dpi));
            var scaleX = bitmap.Width / widthPts;
            var scaleY = bitmap.Height / heightPts;

            using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var pix = Pix.LoadFromMemory(encoded.ToArray());
            using var recognized = engine.Process(pix);

            lines.AddRange(BuildLines(number, recognized, heightPts, scaleX, scaleY));
        }

        return lines;
    }

    /// <summary>Walks the recognized page word-by-word, emitting one <see cref="PdfLine" /> per OCR text line.</summary>
    private static IEnumerable<PdfLine> BuildLines(int pageNumber, Page recognized, double heightPts,
        double scaleX, double scaleY)
    {
        var result = new List<PdfLine>();
        using var iter = recognized.GetIterator();
        iter.Begin();

        var text = new StringBuilder();
        var boxes = new List<CharBox?>();
        var lineHasContent = false;

        do
        {
            // A new text line flushes the one accumulated so far.
            if (iter.IsAtBeginningOf(PageIteratorLevel.TextLine) && lineHasContent)
            {
                result.Add(new PdfLine(pageNumber, text.ToString(), boxes.ToArray()));
                text = new StringBuilder();
                boxes = new List<CharBox?>();
                lineHasContent = false;
            }

            var word = iter.GetText(PageIteratorLevel.Word);
            if (string.IsNullOrWhiteSpace(word) ||
                !iter.TryGetBoundingBox(PageIteratorLevel.Word, out var rect))
                continue;

            if (lineHasContent)
            {
                // Words are separated by a single space that has no glyph of its own.
                text.Append(' ');
                boxes.Add(null);
            }

            AppendWord(text, boxes, word, rect, heightPts, scaleX, scaleY);
            lineHasContent = true;
        }
        while (iter.Next(PageIteratorLevel.Word));

        if (lineHasContent)
            result.Add(new PdfLine(pageNumber, text.ToString(), boxes.ToArray()));

        return result;
    }

    /// <summary>
    ///     Appends a recognized word to the line, converting its pixel bounding box (top-left origin) to PDF
    ///     points (bottom-left origin) and splitting it evenly per character so partial-word spans still locate.
    /// </summary>
    private static void AppendWord(StringBuilder text, List<CharBox?> boxes, string word, Rect rect,
        double heightPts, double scaleX, double scaleY)
    {
        var left = rect.X1 / scaleX;
        var right = rect.X2 / scaleX;
        // Image Y grows downward from the top; PDF Y grows upward from the bottom, so flip about the page height.
        var top = heightPts - rect.Y1 / scaleY;
        var bottom = heightPts - rect.Y2 / scaleY;

        var width = right - left;
        for (var i = 0; i < word.Length; i++)
        {
            var charLeft = left + width * i / word.Length;
            var charRight = left + width * (i + 1) / word.Length;
            boxes.Add(new CharBox(charLeft, bottom, charRight, top));
        }

        text.Append(word);
    }
}
