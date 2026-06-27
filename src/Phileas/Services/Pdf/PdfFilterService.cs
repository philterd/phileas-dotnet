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
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Services.Pdf;

/// <summary>
///     Detects and redacts PII in a PDF document. Text is extracted line-by-line (with coordinates), each
///     line is run through the normal <see cref="FilterService" /> detection pipeline, each detected span
///     is augmented with its page number and bounding box, and the document is then rendered to redacted
///     images. Mirrors the Java <c>PdfFilterService</c>.
/// </summary>
public sealed class PdfFilterService
{
    private readonly FilterService _filterService;
    private readonly ITextExtractor _textExtractor;
    private readonly PdfRedactor _redactor;

    /// <summary>Creates a PDF filter service.</summary>
    /// <param name="filterService">The text detection pipeline (defaults to a new <see cref="FilterService" />).</param>
    /// <param name="textExtractor">The text extractor (defaults to <see cref="PdfTextExtractor" />).</param>
    public PdfFilterService(FilterService? filterService = null, ITextExtractor? textExtractor = null)
    {
        _filterService = filterService ?? new FilterService();
        _textExtractor = textExtractor ?? new PdfTextExtractor();
        _redactor = new PdfRedactor();
    }

    /// <summary>Detects PII in the document and returns the redacted bytes plus the detected spans.</summary>
    /// <param name="policy">The policy.</param>
    /// <param name="context">The context identifier.</param>
    /// <param name="input">The source PDF bytes.</param>
    /// <param name="outputMimeType">The desired output format.</param>
    public BinaryDocumentFilterResult Filter(PhileasPolicy policy, string context, byte[] input,
        MimeType outputMimeType)
    {
        var lines = _textExtractor.GetLines(input);

        var spans = new List<Span>();
        long tokens = 0;
        var piece = 0;

        // Detect once per page rather than once per line. The text of every line on a page is
        // concatenated (newline-separated) into a single detection pass, and each detected span is mapped
        // back to the line that contains it for its bounding box. This collapses many detector
        // invocations into one per page — a large win for the on-device name model (GLiNER), whose
        // per-call cost dominates, since it already chunks long text internally. Spans are still located
        // with line-relative offsets, exactly as before, so a span that straddles a line break is skipped
        // (it could never be located on a single line under the previous per-line approach either).
        foreach (var pageGroup in lines.GroupBy(line => line.PageNumber))
        {
            var pageLines = pageGroup.ToList();

            var builder = new StringBuilder();
            var segments = new List<(int Start, PdfLine Line)>(pageLines.Count);
            foreach (var line in pageLines)
            {
                if (builder.Length > 0)
                    builder.Append('\n');
                segments.Add((builder.Length, line));
                builder.Append(line.Text ?? string.Empty);
            }

            var pageText = builder.ToString();
            if (string.IsNullOrWhiteSpace(pageText))
                continue;

            var result = _filterService.Filter(policy, context, piece++, pageText);
            tokens += result.Tokens;

            foreach (var span in result.Spans)
            {
                var segment = FindContainingSegment(segments, span);
                if (segment is null)
                    continue;

                // Convert the page-relative offsets to line-relative ones so the glyph lookup locates it.
                span.CharacterStart -= segment.Value.Start;
                span.CharacterEnd -= segment.Value.Start;
                if (TryLocate(segment.Value.Line, span))
                    spans.Add(span);
            }
        }

        var redacted = _redactor.Process(input, spans, policy.Config.Pdf, policy.Graphical.BoundingBoxes,
            outputMimeType);

        return new BinaryDocumentFilterResult(redacted, context, spans, tokens);
    }

    /// <summary>Redacts the document using a pre-computed set of spans (which must carry page/coordinates).</summary>
    public byte[] Apply(PhileasPolicy policy, byte[] input, IList<Span> spans, MimeType outputMimeType)
    {
        return _redactor.Process(input, spans, policy.Config.Pdf, policy.Graphical.BoundingBoxes, outputMimeType);
    }

    /// <summary>
    ///     Finds the line segment whose page-text range wholly contains the span; returns null when the
    ///     span straddles a line boundary (and so cannot be placed on a single line's glyphs).
    /// </summary>
    private static (int Start, PdfLine Line)? FindContainingSegment(
        IReadOnlyList<(int Start, PdfLine Line)> segments, Span span)
    {
        foreach (var segment in segments)
        {
            var segmentEnd = segment.Start + (segment.Line.Text?.Length ?? 0);
            if (span.CharacterStart >= segment.Start && span.CharacterEnd <= segmentEnd)
                return segment;
        }

        return null;
    }

    /// <summary>
    ///     Sets the span's page number and bounding box from the glyphs it covers on the line. Returns
    ///     <see langword="false" /> when the span covers no positioned glyphs (and so cannot be located).
    /// </summary>
    private static bool TryLocate(PdfLine line, Span span)
    {
        var start = Math.Clamp(span.CharacterStart, 0, line.Text.Length);
        var end = Math.Clamp(span.CharacterEnd, 0, line.Text.Length);

        double left = 0, right = 0, bottom = 0, top = 0;
        var located = false;

        for (var i = start; i < end; i++)
        {
            var letter = line.LettersByChar[i];
            if (letter == null)
                continue;

            var box = letter.BoundingBox;
            if (!located)
            {
                left = box.Left;
                right = box.Right;
                bottom = box.Bottom;
                top = box.Top;
                located = true;
            }
            else
            {
                left = Math.Min(left, box.Left);
                right = Math.Max(right, box.Right);
                bottom = Math.Min(bottom, box.Bottom);
                top = Math.Max(top, box.Top);
            }
        }

        if (!located)
            return false;

        span.PageNumber = line.PageNumber;
        span.LowerLeftX = left;
        span.LowerLeftY = bottom;
        span.UpperRightX = right;
        span.UpperRightY = top;
        return true;
    }
}
