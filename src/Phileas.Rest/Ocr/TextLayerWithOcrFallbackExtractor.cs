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

namespace Phileas.Rest.Ocr;

/// <summary>
///     An <see cref="ITextExtractor" /> that prefers the PDF text layer and falls back to OCR only for pages
///     that have no extractable text (typically scanned pages). This is both faster and more accurate than
///     OCR-everything on documents that mix native and scanned pages.
/// </summary>
public sealed class TextLayerWithOcrFallbackExtractor : ITextExtractor
{
    private readonly ITextExtractor _textLayer;
    private readonly TesseractTextExtractor _ocr;

    public TextLayerWithOcrFallbackExtractor(ITextExtractor textLayer, TesseractTextExtractor ocr)
    {
        _textLayer = textLayer;
        _ocr = ocr;
    }

    /// <inheritdoc />
    public IReadOnlyList<PdfLine> GetLines(byte[] document)
    {
        var textLines = _textLayer.GetLines(document);
        var pagesWithText = textLines.Select(l => l.PageNumber).ToHashSet();

        int pageCount;
        using (var pdf = PdfDocument.Open(document))
            pageCount = pdf.NumberOfPages;

        var missing = Enumerable.Range(1, pageCount).Where(p => !pagesWithText.Contains(p)).ToHashSet();
        if (missing.Count == 0)
            return textLines;

        var ocrLines = _ocr.Extract(document, missing);
        return textLines.Concat(ocrLines).OrderBy(l => l.PageNumber).ToList();
    }
}
