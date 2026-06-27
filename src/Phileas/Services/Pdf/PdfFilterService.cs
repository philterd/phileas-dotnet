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
        // back to the line(s) it covers for its bounding box. This collapses many detector invocations
        // into one per page — a large win for the on-device name model (GLiNER), whose per-call cost
        // dominates, since it already chunks long text internally. A span that straddles a line break
        // (e.g. a name wrapped across two lines) is split into one located box per line it covers, so it
        // is still fully redacted.
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
                spans.AddRange(LocatePerLine(span, segments));
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
    ///     Maps a page-relative span onto the line(s) it covers, yielding one located span per line. A span
    ///     wholly within one line produces a single box; a span that straddles a line break is split so each
    ///     line's portion gets its own box (so a name wrapped across two lines is still fully redacted).
    ///     Portions that cover no positioned glyphs are dropped.
    /// </summary>
    private static IEnumerable<Span> LocatePerLine(Span span, IReadOnlyList<(int Start, PdfLine Line)> segments)
    {
        var lineRanges = segments.Select(s => (s.Start, Length: s.Line.Text?.Length ?? 0)).ToList();
        foreach (var (segmentIndex, localStart, localEnd) in SplitSpanAcrossLines(span.CharacterStart, span.CharacterEnd, lineRanges))
        {
            var line = segments[segmentIndex].Line;
            var portion = span.Copy();
            portion.CharacterStart = localStart;
            portion.CharacterEnd = localEnd;
            portion.Text = line.Text!.Substring(localStart, localEnd - localStart);
            if (TryLocate(line, portion))
                yield return portion;
        }
    }

    /// <summary>
    ///     Splits a span (given by its page-text offsets) into per-line portions, one for each line whose
    ///     range overlaps the span, returning the line index and the span's offsets expressed within that
    ///     line. A span within a single line yields one portion; a span straddling a line break yields one
    ///     portion per line it covers; the newline separators between lines are never included.
    /// </summary>
    internal static IEnumerable<(int SegmentIndex, int LocalStart, int LocalEnd)> SplitSpanAcrossLines(
        int spanStart, int spanEnd, IReadOnlyList<(int Start, int Length)> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var (start, length) = lines[i];
            var overlapStart = Math.Max(spanStart, start);
            var overlapEnd = Math.Min(spanEnd, start + length);
            if (overlapEnd > overlapStart)
                yield return (i, overlapStart - start, overlapEnd - start);
        }
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
