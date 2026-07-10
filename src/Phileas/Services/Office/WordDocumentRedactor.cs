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

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Phileas.Model;
using A = DocumentFormat.OpenXml.Drawing;

namespace Phileas.Services.Office
{
    /// <summary>
    /// Redacts Microsoft Word (.docx) documents using the open-source Open XML SDK
    /// (<c>DocumentFormat.OpenXml</c>) — no commercial dependency or license key.
    ///
    /// Each paragraph's text is run through the supplied filter and, when the filter changes it, the
    /// paragraph is rebuilt from the resulting spans. Working at the paragraph level preserves the
    /// document's structure. The applied spans are captured (with a stable paragraph index) so they
    /// can be stored and later re-applied via <c>ApplySpans</c>.
    ///
    /// Note: rebuilding a <em>changed</em> paragraph flattens its inline run formatting (and any
    /// hyperlinks/fields collapse to plain text), since the visible text is what gets redacted.
    /// Unchanged paragraphs are left exactly as they were.
    /// </summary>
    public static class WordDocumentRedactor
    {
        private const string DefaultReplacement = "{{{REDACTED-custom}}}";

        // A PII-bearing hyperlink target is rewritten to this. ".invalid" is reserved (RFC 2606) so it
        // never resolves; the relationship keeps its id, so the (already redacted) link text stays valid.
        private const string RedactedHyperlinkTarget = "https://redacted.invalid/";
        private const string HyperlinkClassification = "hyperlink-target";
        private const string FieldInstructionClassification = "field-instruction";

        /// <summary>
        /// Returns the text of each redactable paragraph (body, then headers/footers) in the canonical
        /// order used for redaction — i.e. index <c>i</c> here is <see cref="OfficeRedactionSpan.ParagraphIndex"/>
        /// <c>i</c>. Read-only; does not modify the file. Used for the preview workspace.
        /// </summary>
        public static List<string> ReadParagraphs(string inputPath) => ReadParagraphs(File.ReadAllBytes(inputPath));

        /// <summary>The <c>byte[]</c> overload of <see cref="ReadParagraphs(string)"/>, for redacting in-memory document bytes.</summary>
        public static List<string> ReadParagraphs(byte[] input)
        {
            using MemoryStream buffer = SafeOutput.ToEditableStream(input);
            using WordprocessingDocument document = WordprocessingDocument.Open(buffer, isEditable: false);
            return EnumerateTargets(document).Select(t => OwnText(t.Paragraph)).ToList();
        }

        /// <summary>
        /// Returns every readable line of a .docx for a before/after review diff: the redactable
        /// paragraphs (body, headers/footers, notes, comments — as <c>ReadParagraphs</c>) plus the
        /// DrawingML text of shapes, SmartArt, and chart labels, which redaction also rewrites. Unlike
        /// <c>ReadParagraphs</c>, this is <b>not</b> paragraph-index aligned — it exists so the diff
        /// doesn't understate redactions by hiding shape/SmartArt/chart changes. Read-only.
        /// </summary>
        public static List<string> ReadReviewLines(string inputPath) => ReadReviewLines(File.ReadAllBytes(inputPath));

        /// <summary>The <c>byte[]</c> overload of <see cref="ReadReviewLines(string)"/>, for in-memory document bytes.</summary>
        public static List<string> ReadReviewLines(byte[] input)
        {
            using MemoryStream buffer = SafeOutput.ToEditableStream(input);
            using WordprocessingDocument document = WordprocessingDocument.Open(buffer, isEditable: false);
            var lines = EnumerateTargets(document).Select(t => OwnText(t.Paragraph)).ToList();
            lines.AddRange(ReadDrawingParagraphText(document));
            return lines;
        }

        // The concatenated text of each DrawingML paragraph across every part, in the same AllParts walk
        // order RedactDrawingText uses — redaction preserves the drawing structure (it flattens each
        // paragraph's text into its first run without removing paragraphs), so source and output align.
        private static IEnumerable<string> ReadDrawingParagraphText(WordprocessingDocument document)
        {
            MainDocumentPart? main = document.MainDocumentPart;
            if (main is null)
            {
                yield break;
            }

            foreach (OpenXmlPart part in AllParts(main))
            {
                OpenXmlElement? root;
                try
                {
                    root = part.RootElement; // may throw on binary/VML/untyped parts — skip those
                }
                catch
                {
                    continue;
                }
                if (root is null)
                {
                    continue;
                }
                foreach (A.Paragraph paragraph in root.Descendants<A.Paragraph>())
                {
                    List<A.Text> texts = paragraph.Descendants<A.Text>().ToList();
                    if (texts.Count > 0)
                    {
                        yield return string.Concat(texts.Select(t => t.Text));
                    }
                }
            }
        }

        /// <summary>
        /// Detects redactions for <paramref name="inputPath"/> using <paramref name="filter"/> without
        /// writing anything, returning the spans (paragraph-indexed) — the same set <c>Redact</c>
        /// would apply. Used for the preview workspace.
        /// </summary>
        public static List<OfficeRedactionSpan> Detect(string inputPath, Func<string, TextFilterResult> filter, bool redactHeadersFooters = true, bool redactCharts = true) =>
            Detect(File.ReadAllBytes(inputPath), filter, redactHeadersFooters, redactCharts);

        /// <summary>
        /// The <c>byte[]</c> overload of <see cref="Detect(string, Func{string, TextFilterResult}, bool, bool)"/>:
        /// detects redactions in the in-memory document bytes without producing any output.
        /// </summary>
        public static List<OfficeRedactionSpan> Detect(byte[] input, Func<string, TextFilterResult> filter, bool redactHeadersFooters = true, bool redactCharts = true)
        {
            using MemoryStream buffer = SafeOutput.ToEditableStream(input);
            using WordprocessingDocument document = WordprocessingDocument.Open(buffer, isEditable: false);
            List<OfficeRedactionSpan> captured = DetectOpenWordDocument(document, filter, redactHeadersFooters, redactCharts);
            int embedOrder = captured.Count;
            EmbeddedObjectRedactor.Process(document, filter, removeUninspectable: false, write: false, captured, ref embedOrder);
            return captured;
        }

        private static List<OfficeRedactionSpan> DetectOpenWordDocument(
            WordprocessingDocument document, Func<string, TextFilterResult> filter,
            bool redactHeadersFooters = true, bool redactCharts = true)
        {
            var captured = new List<OfficeRedactionSpan>();
            int order = 0;
            int paragraphIndex = 0;
            foreach ((Paragraph paragraph, bool isHeaderFooter) in EnumerateTargets(document))
            {
                string original = OwnText(paragraph);
                if (!string.IsNullOrEmpty(original) && (redactHeadersFooters || !isHeaderFooter))
                {
                    foreach (Span s in filter(original).Spans
                        .Where(s => s.CharacterStart >= 0 && s.CharacterEnd <= original.Length && s.CharacterEnd > s.CharacterStart)
                        .OrderBy(s => s.CharacterStart))
                    {
                        var entity = new OfficeRedactionSpan
                        {
                            Order = order++,
                            ParagraphIndex = paragraphIndex,
                            CharacterStart = s.CharacterStart,
                            CharacterEnd = s.CharacterEnd,
                            Text = original.Substring(s.CharacterStart, s.CharacterEnd - s.CharacterStart),
                            Replacement = s.Replacement ?? string.Empty,
                            Classification = s.Classification ?? string.Empty
                        };
                        OfficeSpanExplanation.Populate(entity, s);
                        captured.Add(entity);
                    }
                }
                paragraphIndex++;
            }
            // Deleted tracked-change text (w:delText) isn't a body paragraph, so scan it too — otherwise
            // the preview/verification would miss residual PII in a tracked deletion.
            captured.AddRange(ScanDeletedText(document, filter, write: false, ref order));
            // Field instruction text (HYPERLINK/INCLUDETEXT/merge sources) isn't a body paragraph either.
            captured.AddRange(RedactFieldInstructions(document, filter, write: false, ref order));
            // Chart title/label/cached-value text likewise isn't a body paragraph.
            if (redactCharts)
            {
                captured.AddRange(ScanCharts(document, filter, write: false, ref order));
            }
            return captured;
        }

        /// <summary>
        /// Loads <paramref name="inputPath"/>, redacts its text with <paramref name="filter"/>, writes
        /// the result to <paramref name="outputPath"/>, and returns the applied spans (paragraph-indexed).
        /// The input file is left untouched.
        /// </summary>
        public static List<OfficeRedactionSpan> Redact(string inputPath, string outputPath, Func<string, TextFilterResult> filter, bool highlight = false, bool redactHeadersFooters = true, bool redactCharts = true, bool removeEmbeddedObjects = true)
        {
            // Redact in memory, then write the output once so a failure never leaves the original or a
            // partial file.
            (byte[] document, List<OfficeRedactionSpan> captured) = Redact(
                File.ReadAllBytes(inputPath), filter, highlight, redactHeadersFooters, redactCharts, removeEmbeddedObjects);
            SafeOutput.Write(outputPath, document);
            return captured;
        }

        /// <summary>
        /// Redacts the in-memory document bytes with <paramref name="filter"/> and returns the redacted
        /// document bytes plus the applied spans (paragraph-indexed). The <c>byte[]</c> overload of
        /// <see cref="Redact(string, string, Func{string, TextFilterResult}, bool, bool, bool, bool)"/>,
        /// for callers (a service, a stream pipeline) that never touch the file system.
        /// </summary>
        public static (byte[] Document, List<OfficeRedactionSpan> Spans) Redact(byte[] input, Func<string, TextFilterResult> filter, bool highlight = false, bool redactHeadersFooters = true, bool redactCharts = true, bool removeEmbeddedObjects = true)
        {
            using MemoryStream buffer = SafeOutput.ToEditableStream(input);
            using WordprocessingDocument document = WordprocessingDocument.Open(buffer, isEditable: true);
            List<OfficeRedactionSpan> captured = RedactOpenWordDocument(document, filter, highlight, redactHeadersFooters, redactCharts);
            // Embedded objects (Insert > Object) carry their own content — recurse into embedded Word/Excel
            // documents, and remove opaque objects we can't inspect. Top level only, so it can't loop endlessly.
            int embedOrder = captured.Count;
            EmbeddedObjectRedactor.Process(document, filter, removeEmbeddedObjects, write: true, captured, ref embedOrder);
            document.Save(); // flush into the buffer
            return (buffer.ToArray(), captured);
        }

        // The in-memory Word redaction core, shared by Redact(path) and the embedded-document recursion. Does
        // NOT itself descend into embedded objects — that pass runs only at the top level.
        internal static List<OfficeRedactionSpan> RedactOpenWordDocument(
            WordprocessingDocument document, Func<string, TextFilterResult> filter, bool highlight,
            bool redactHeadersFooters, bool redactCharts)
        {
            var captured = new List<OfficeRedactionSpan>();
            int order = 0;
            int paragraphIndex = 0;

            foreach ((Paragraph paragraph, bool isHeaderFooter) in EnumerateTargets(document).ToList())
            {
                string original = OwnText(paragraph);
                if (!string.IsNullOrEmpty(original) && (redactHeadersFooters || !isHeaderFooter))
                {
                    TextFilterResult result = filter(original);
                    if (!string.Equals(result.FilteredText, original, StringComparison.Ordinal))
                    {
                        List<Span> spans = result.Spans
                            .Where(s => s.CharacterStart >= 0 && s.CharacterEnd <= original.Length && s.CharacterEnd > s.CharacterStart)
                            .OrderBy(s => s.CharacterStart)
                            .ToList();

                        // Resolve overlaps before rebuilding (parity with ApplySpans): an unresolved
                        // overlapping range would be silently skipped while rebuilding, which could leave
                        // original text behind. The engine doesn't emit overlaps today; this is defensive.
                        List<ReplacementRange> ranges = OfficeSpanMath.ResolveNonOverlapping(
                            spans.Select(s => new ReplacementRange(s.CharacterStart, s.CharacterEnd, s.Replacement ?? string.Empty)));

                        ApplyRangesToParagraph(paragraph, original, ranges, highlight);

                        foreach (Span s in spans)
                        {
                            var entity = new OfficeRedactionSpan
                            {
                                Order = order++,
                                ParagraphIndex = paragraphIndex,
                                CharacterStart = s.CharacterStart,
                                CharacterEnd = s.CharacterEnd,
                                Text = original.Substring(s.CharacterStart, s.CharacterEnd - s.CharacterStart),
                                Replacement = s.Replacement ?? string.Empty,
                                Classification = s.Classification ?? string.Empty
                            };
                            OfficeSpanExplanation.Populate(entity, s);
                            captured.Add(entity);
                        }
                    }
                }
                paragraphIndex++;
            }

            captured.AddRange(RedactDrawingText(document, filter, ref order));
            captured.AddRange(RedactHyperlinkTargets(document, filter, ref order));
            captured.AddRange(RedactFieldInstructions(document, filter, write: true, ref order));
            captured.AddRange(ScanDeletedText(document, filter, write: true, ref order));
            if (redactCharts)
            {
                captured.AddRange(ScanCharts(document, filter, write: true, ref order));
            }
            RemoveThreadedCommentDuplicate(document);
            return captured;
        }

        // Redacts an embedded .docx's bytes with the standard passes (no further embedded-object recursion).
        // Returns the redacted bytes, or null when the package can't be opened as a Word document.
        internal static byte[]? RedactEmbeddedBytes(byte[] bytes, Func<string, TextFilterResult> filter, out List<OfficeRedactionSpan> spans)
        {
            spans = new List<OfficeRedactionSpan>();
            try
            {
                using var ms = new MemoryStream();
                ms.Write(bytes, 0, bytes.Length);
                ms.Position = 0;
                using (WordprocessingDocument document = WordprocessingDocument.Open(ms, isEditable: true))
                {
                    if (document.MainDocumentPart is null)
                    {
                        return null;
                    }
                    spans = RedactOpenWordDocument(document, filter, highlight: false, redactHeadersFooters: true, redactCharts: true);
                    document.Save();
                }
                return ms.ToArray();
            }
            catch
            {
                spans = new List<OfficeRedactionSpan>();
                return null;
            }
        }

        internal static List<OfficeRedactionSpan> DetectEmbeddedBytes(byte[] bytes, Func<string, TextFilterResult> filter)
        {
            try
            {
                using var ms = new MemoryStream();
                ms.Write(bytes, 0, bytes.Length);
                ms.Position = 0;
                using WordprocessingDocument document = WordprocessingDocument.Open(ms, isEditable: false);
                if (document.MainDocumentPart is null)
                {
                    return new List<OfficeRedactionSpan>();
                }
                return DetectOpenWordDocument(document, filter);
            }
            catch
            {
                return new List<OfficeRedactionSpan>();
            }
        }

        /// <summary>
        /// Applies an explicit set of spans to <paramref name="inputPath"/>, writing to
        /// <paramref name="outputPath"/>. Every span — detected or user-added — is applied by
        /// <b>position</b>: its <see cref="OfficeRedactionSpan.ParagraphIndex"/> plus character
        /// start/stop offsets within that paragraph.
        /// </summary>
        public static void ApplySpans(string inputPath, string outputPath, IReadOnlyList<OfficeRedactionSpan> spans, bool highlight,
            Func<string, TextFilterResult>? drawingFilter = null, bool redactCharts = true, bool removeEmbeddedObjects = true)
        {
            byte[] document = ApplySpans(File.ReadAllBytes(inputPath), spans, highlight, drawingFilter, redactCharts, removeEmbeddedObjects);
            SafeOutput.Write(outputPath, document);
        }

        /// <summary>
        /// The <c>byte[]</c> overload of
        /// <see cref="ApplySpans(string, string, IReadOnlyList{OfficeRedactionSpan}, bool, Func{string, TextFilterResult}, bool, bool)"/>:
        /// applies the spans to the in-memory document bytes and returns the redacted bytes.
        /// </summary>
        public static byte[] ApplySpans(byte[] input, IReadOnlyList<OfficeRedactionSpan> spans, bool highlight,
            Func<string, TextFilterResult>? drawingFilter = null, bool redactCharts = true, bool removeEmbeddedObjects = true)
        {
            Dictionary<int, List<OfficeRedactionSpan>> byParagraph = spans
                .GroupBy(s => s.ParagraphIndex)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Apply in memory, then return the bytes (the path overload writes them once; see Redact).
            using MemoryStream buffer = SafeOutput.ToEditableStream(input);
            using WordprocessingDocument document = WordprocessingDocument.Open(buffer, isEditable: true);

            int paragraphIndex = 0;
            foreach ((Paragraph paragraph, bool _) in EnumerateTargets(document).ToList())
            {
                string original = OwnText(paragraph);
                if (!string.IsNullOrEmpty(original) && byParagraph.TryGetValue(paragraphIndex, out List<OfficeRedactionSpan>? paragraphSpans))
                {
                    var ranges = new List<ReplacementRange>();
                    foreach (OfficeRedactionSpan s in paragraphSpans)
                    {
                        if (s.CharacterStart >= 0 && s.CharacterEnd <= original.Length && s.CharacterEnd > s.CharacterStart)
                        {
                            string repl = string.IsNullOrEmpty(s.Replacement) ? DefaultReplacement : s.Replacement;
                            ranges.Add(new ReplacementRange(s.CharacterStart, s.CharacterEnd, repl));
                        }
                    }

                    List<ReplacementRange> resolved = OfficeSpanMath.ResolveNonOverlapping(ranges);
                    ApplyRangesToParagraph(paragraph, original, resolved, highlight);
                }
                paragraphIndex++;
            }

            if (drawingFilter is not null)
            {
                // Re-render path: re-redact drawing/hyperlink text via the filter (stored spans re-apply the
                // body by position). The captured drawing spans are already in the stored history, so the
                // returns are discarded here.
                int order = 0;
                RedactDrawingText(document, drawingFilter, ref order);
                RedactHyperlinkTargets(document, drawingFilter, ref order);
                RedactFieldInstructions(document, drawingFilter, write: true, ref order);
                ScanDeletedText(document, drawingFilter, write: true, ref order);
                if (redactCharts)
                {
                    ScanCharts(document, drawingFilter, write: true, ref order);
                }
                EmbeddedObjectRedactor.Process(document, drawingFilter, removeEmbeddedObjects, write: true, captured: null, ref order);
            }
            RemoveThreadedCommentDuplicate(document);
            document.Save(); // flush into the buffer
            return buffer.ToArray();
        }

        /// <summary>
        /// Canonical, stable order of redactable paragraphs: body (including tables), then header and
        /// footer parts in First, Even, Default(Odd) order, then footnotes, endnotes, and comments. The
        /// order must match between <c>Redact</c> and <c>ApplySpans</c> so a stored
        /// paragraph index re-applies.
        /// </summary>
        private static IEnumerable<(Paragraph Paragraph, bool IsHeaderFooter)> EnumerateTargets(WordprocessingDocument document)
        {
            MainDocumentPart? main = document.MainDocumentPart;
            if (main is null)
            {
                yield break;
            }

            Body? body = main.Document?.Body;
            if (body is not null)
            {
                foreach (Paragraph p in body.Descendants<Paragraph>())
                {
                    yield return (p, false);
                }
            }

            foreach (OpenXmlPart part in OrderedHeaderFooterParts(main))
            {
                if (part.RootElement is OpenXmlElement root)
                {
                    foreach (Paragraph p in root.Descendants<Paragraph>())
                    {
                        yield return (p, true);
                    }
                }
            }

            // Footnotes then endnotes: their paragraphs carry body-like text, so they must be filtered
            // like any other paragraph — otherwise PII in a note ships in the output.
            if (main.FootnotesPart?.Footnotes is Footnotes footnotes)
            {
                foreach (Paragraph p in footnotes.Descendants<Paragraph>())
                {
                    yield return (p, false);
                }
            }
            if (main.EndnotesPart?.Endnotes is Endnotes endnotes)
            {
                foreach (Paragraph p in endnotes.Descendants<Paragraph>())
                {
                    yield return (p, false);
                }
            }

            // Comment text: redact it too, so PII in a comment isn't shipped when the user keeps
            // comments (they're only deleted separately, by the "remove comments" scrub). Appended last
            // so body/header/footer paragraph indices stay stable for stored-span re-apply.
            if (main.WordprocessingCommentsPart?.Comments is Comments comments)
            {
                foreach (Paragraph p in comments.Descendants<Paragraph>())
                {
                    yield return (p, false);
                }
            }
        }

        // Redacts DrawingML text (<a:t> runs in shapes, SmartArt, and charts) — which is not made of
        // WordprocessingML <w:p> paragraphs and so is missed by the body/notes enumeration. Each DrawingML
        // paragraph's runs are concatenated, filtered, and (when changed) flattened into its first run so
        // PII in a shape/SmartArt/chart label doesn't survive. Walks every part of the package so text in
        // the main document, headers/footers, notes, chart parts, and SmartArt data is covered. Returns the
        // redactions it made so they're recorded in the report/explanation like any other span.
        private static List<OfficeRedactionSpan> RedactDrawingText(
            WordprocessingDocument document, Func<string, TextFilterResult> filter, ref int order)
        {
            var captured = new List<OfficeRedactionSpan>();
            MainDocumentPart? main = document.MainDocumentPart;
            if (main is null)
            {
                return captured;
            }

            foreach (OpenXmlPart part in AllParts(main))
            {
                if (part is ChartPart)
                {
                    continue; // charts are handled by the (settings-gated) chart pass, incl. cached values
                }
                OpenXmlElement? root;
                try
                {
                    // Accessing RootElement parses the part; some parts (binary, VML, or a version the
                    // SDK can't type) throw — skip them rather than fail the whole redaction.
                    root = part.RootElement;
                }
                catch
                {
                    continue;
                }
                if (root is null)
                {
                    continue;
                }
                foreach (A.Paragraph paragraph in root.Descendants<A.Paragraph>().ToList())
                {
                    RedactDrawingParagraph(paragraph, filter, ref order, captured);
                }
            }
            return captured;
        }

        private static void RedactDrawingParagraph(
            A.Paragraph paragraph, Func<string, TextFilterResult> filter, ref int order, List<OfficeRedactionSpan> captured)
        {
            List<A.Text> texts = paragraph.Descendants<A.Text>().ToList();
            if (texts.Count == 0)
            {
                return;
            }

            string original = string.Concat(texts.Select(t => t.Text));
            if (string.IsNullOrEmpty(original))
            {
                return;
            }

            TextFilterResult result = filter(original);
            if (string.Equals(result.FilteredText, original, StringComparison.Ordinal))
            {
                return;
            }

            // Flatten the (possibly multi-run) text into the first run, clearing the rest — the same
            // approach the WordprocessingML rebuild uses; the run structure (and the drawing) is preserved.
            texts[0].Text = result.FilteredText;
            for (int i = 1; i < texts.Count; i++)
            {
                texts[i].Text = string.Empty;
            }

            // Record each detection so shape/SmartArt/chart redactions appear in the report count, the
            // "What was removed" table, and the explanation export. ParagraphIndex -1: not a body paragraph.
            foreach (Span s in result.Spans
                         .Where(s => s.CharacterStart >= 0 && s.CharacterEnd <= original.Length && s.CharacterEnd > s.CharacterStart)
                         .OrderBy(s => s.CharacterStart))
            {
                var entity = new OfficeRedactionSpan
                {
                    Order = order++,
                    ParagraphIndex = -1,
                    CharacterStart = s.CharacterStart,
                    CharacterEnd = s.CharacterEnd,
                    Text = original.Substring(s.CharacterStart, s.CharacterEnd - s.CharacterStart),
                    Replacement = s.Replacement ?? string.Empty,
                    Classification = s.Classification ?? string.Empty
                };
                OfficeSpanExplanation.Populate(entity, s);
                captured.Add(entity);
            }
        }

        // Redacts the text of tracked *deletions* (w:delText). Word keeps deleted-but-tracked text in
        // <w:delText>, not <w:t>, so the body/notes enumeration (which reads only <w:t>) misses it — the PII
        // would ship verbatim and is recoverable with "Reject Changes". This runs the filter over each
        // deleted-text element and replaces detected PII in place, keeping the run a tracked deletion, so
        // the leak is closed regardless of the (toggleable) tracked-changes scrub and on the Modify path.
        // Each element is filtered on its own (a deletion's text is a contiguous run); spans use
        // ParagraphIndex -1 (not a body paragraph). With write=false it only detects (preview/verification).
        // Every part is walked, so deletions in the body, headers/footers, notes, and comments are covered.
        private static List<OfficeRedactionSpan> ScanDeletedText(
            WordprocessingDocument document, Func<string, TextFilterResult> filter, bool write, ref int order)
        {
            var captured = new List<OfficeRedactionSpan>();
            MainDocumentPart? main = document.MainDocumentPart;
            if (main is null)
            {
                return captured;
            }

            foreach (OpenXmlPart part in AllParts(main))
            {
                OpenXmlElement? root;
                try
                {
                    root = part.RootElement; // parses the part; skip parts the SDK can't type
                }
                catch
                {
                    continue;
                }
                if (root is null)
                {
                    continue;
                }
                foreach (DeletedText deleted in root.Descendants<DeletedText>().ToList())
                {
                    string original = deleted.Text ?? string.Empty;
                    if (string.IsNullOrEmpty(original))
                    {
                        continue;
                    }
                    TextFilterResult result = filter(original);
                    if (string.Equals(result.FilteredText, original, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (write)
                    {
                        deleted.Text = result.FilteredText;
                    }
                    foreach (Span s in result.Spans
                                 .Where(s => s.CharacterStart >= 0 && s.CharacterEnd <= original.Length && s.CharacterEnd > s.CharacterStart)
                                 .OrderBy(s => s.CharacterStart))
                    {
                        var entity = new OfficeRedactionSpan
                        {
                            Order = order++,
                            ParagraphIndex = -1,
                            CharacterStart = s.CharacterStart,
                            CharacterEnd = s.CharacterEnd,
                            Text = original.Substring(s.CharacterStart, s.CharacterEnd - s.CharacterStart),
                            Replacement = s.Replacement ?? string.Empty,
                            Classification = s.Classification ?? string.Empty
                        };
                        OfficeSpanExplanation.Populate(entity, s);
                        captured.Add(entity);
                    }
                }
            }
            return captured;
        }

        // Redacts (write: true) or detects (write: false) PII in embedded charts: title/axis/data-label
        // text and the cached series/category values (numCache/strCache) that copy the source cells. Chart
        // parts are excluded from the general drawing pass and handled here so this can be settings-gated.
        private static List<OfficeRedactionSpan> ScanCharts(
            WordprocessingDocument document, Func<string, TextFilterResult> filter, bool write, ref int order)
        {
            var captured = new List<OfficeRedactionSpan>();
            MainDocumentPart? main = document.MainDocumentPart;
            if (main is null)
            {
                return captured;
            }
            foreach (OpenXmlPart part in AllParts(main))
            {
                if (part is ChartPart chartPart)
                {
                    ChartRedactor.RedactChartPart(chartPart, filter, write, captured, ref order);
                    // The chart's embedded source workbook is the authoritative source data (recoverable via
                    // Edit Data), holding more than the plotted cache — recurse the spreadsheet redactor into it.
                    XlsxRedactor.RedactChartEmbeddedWorkbooks(chartPart, filter, write, captured, ref order);
                }
            }
            return captured;
        }

        // Redacts hyperlink URL *targets* — the external addresses stored as relationships and referenced
        // by w:hyperlink r:id / HYPERLINK fields (e.g. mailto:john@x.com, an intranet URL carrying an id,
        // a file:// path). The visible link text is redacted with the body, but the target lives in the
        // part's relationships and would otherwise ship intact. Each external target is run through the
        // policy filter; any target the policy flags is rewritten to a neutral placeholder (keeping the
        // relationship id so the document stays valid). Benign targets are left untouched. Every part is
        // walked, so links in the body, headers/footers, notes, and comments are all covered.
        private static List<OfficeRedactionSpan> RedactHyperlinkTargets(WordprocessingDocument document, Func<string, TextFilterResult> filter, ref int order)
        {
            var captured = new List<OfficeRedactionSpan>();
            MainDocumentPart? main = document.MainDocumentPart;
            if (main is null)
            {
                return captured;
            }

            foreach (OpenXmlPart part in AllParts(main))
            {
                // Snapshot: we delete/re-add relationships on this part inside the loop.
                foreach (HyperlinkRelationship rel in part.HyperlinkRelationships.ToList())
                {
                    if (!rel.IsExternal)
                    {
                        continue; // internal anchors (#bookmark) carry no external address
                    }

                    string target = rel.Uri?.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(target))
                    {
                        continue;
                    }

                    TextFilterResult result = filter(target);
                    if (result.Spans.Count == 0)
                    {
                        continue; // policy found nothing in this target -> keep the link intact
                    }

                    string id = rel.Id;
                    part.DeleteReferenceRelationship(rel);
                    part.AddHyperlinkRelationship(new Uri(RedactedHyperlinkTarget, UriKind.Absolute), isExternal: true, id);

                    var entity = new OfficeRedactionSpan
                    {
                        Order = order++,
                        ParagraphIndex = -1, // a hyperlink target, not a paragraph offset
                        CharacterStart = 0,
                        CharacterEnd = target.Length,
                        Text = target,
                        Replacement = RedactedHyperlinkTarget,
                        Classification = HyperlinkClassification,
                    };
                    OfficeSpanExplanation.Populate(entity, result.Spans[0]);
                    captured.Add(entity);
                }
            }

            return captured;
        }

        // Redacts (write: true) or detects (write: false) PII in Word field instruction text — the part that
        // isn't the visible result and so survives the body pass. Two forms: a simple field's w:instr
        // attribute (SimpleField.Instruction), and a complex field's <w:instrText> runs (FieldCode). Field
        // instructions carry PII in HYPERLINK "mailto:…"/URL targets, INCLUDETEXT file paths, merge sources.
        private static List<OfficeRedactionSpan> RedactFieldInstructions(
            WordprocessingDocument document, Func<string, TextFilterResult> filter, bool write, ref int order)
        {
            var captured = new List<OfficeRedactionSpan>();
            MainDocumentPart? main = document.MainDocumentPart;
            if (main is null)
            {
                return captured;
            }

            foreach (OpenXmlPart part in AllParts(main))
            {
                OpenXmlElement? root;
                try
                {
                    root = part.RootElement; // parses the part; skip parts the SDK can't type
                }
                catch
                {
                    continue;
                }
                if (root is null)
                {
                    continue;
                }

                // Simple fields: the instruction is an attribute (SimpleField.Instruction).
                foreach (SimpleField field in root.Descendants<SimpleField>().ToList())
                {
                    string original = field.Instruction?.Value ?? string.Empty;
                    if (string.IsNullOrEmpty(original))
                    {
                        continue;
                    }
                    TextFilterResult result = filter(original);
                    if (string.Equals(result.FilteredText, original, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (write)
                    {
                        field.Instruction = result.FilteredText;
                    }
                    CaptureFieldSpans(original, result, ref order, captured);
                }

                // Complex fields: instruction is one or more <w:instrText> runs; merge a field's runs so PII
                // split across them is caught, then flatten the result into the first run.
                foreach (List<FieldCode> group in GroupFieldCodes(root))
                {
                    string original = string.Concat(group.Select(c => c.Text));
                    if (string.IsNullOrEmpty(original))
                    {
                        continue;
                    }
                    TextFilterResult result = filter(original);
                    if (string.Equals(result.FilteredText, original, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (write)
                    {
                        group[0].Text = result.FilteredText;
                        for (int i = 1; i < group.Count; i++)
                        {
                            group[i].Text = string.Empty;
                        }
                    }
                    CaptureFieldSpans(original, result, ref order, captured);
                }
            }

            return captured;
        }

        // Groups a complex field's contiguous <w:instrText> runs by walking the field-character state
        // machine (begin … [instruction] … separate/end) so each field's instruction is merged as a unit.
        private static IEnumerable<List<FieldCode>> GroupFieldCodes(OpenXmlElement root)
        {
            var fields = new List<List<FieldCode>>();
            List<FieldCode>? current = null;
            foreach (Run run in root.Descendants<Run>())
            {
                FieldChar? fldChar = run.GetFirstChild<FieldChar>();
                if (fldChar?.FieldCharType?.Value is FieldCharValues type)
                {
                    if (type == FieldCharValues.Begin)
                    {
                        current = new List<FieldCode>();
                    }
                    else // separate or end closes the instruction portion
                    {
                        if (current is { Count: > 0 })
                        {
                            fields.Add(current);
                        }
                        current = null;
                    }
                }
                if (current is not null)
                {
                    current.AddRange(run.Elements<FieldCode>());
                }
            }
            if (current is { Count: > 0 }) // an unterminated field (malformed, but be safe)
            {
                fields.Add(current);
            }
            return fields;
        }

        private static void CaptureFieldSpans(string original, TextFilterResult result, ref int order, List<OfficeRedactionSpan> captured)
        {
            foreach (Span s in result.Spans
                         .Where(s => s.CharacterStart >= 0 && s.CharacterEnd <= original.Length && s.CharacterEnd > s.CharacterStart)
                         .OrderBy(s => s.CharacterStart))
            {
                var entity = new OfficeRedactionSpan
                {
                    Order = order++,
                    ParagraphIndex = -1, // a field instruction, not a paragraph offset
                    CharacterStart = s.CharacterStart,
                    CharacterEnd = s.CharacterEnd,
                    Text = original.Substring(s.CharacterStart, s.CharacterEnd - s.CharacterStart),
                    Replacement = s.Replacement ?? string.Empty,
                    Classification = string.IsNullOrEmpty(s.Classification) ? FieldInstructionClassification : s.Classification
                };
                OfficeSpanExplanation.Populate(entity, s);
                captured.Add(entity);
            }
        }

        // Every distinct part reachable from the main document part (itself included), breadth-first.
        private static IEnumerable<OpenXmlPart> AllParts(MainDocumentPart main)
        {
            var seen = new HashSet<OpenXmlPart> { main };
            var queue = new Queue<OpenXmlPart>();
            queue.Enqueue(main);
            while (queue.Count > 0)
            {
                OpenXmlPart part = queue.Dequeue();
                yield return part;
                foreach (IdPartPair child in part.Parts)
                {
                    if (seen.Add(child.OpenXmlPart))
                    {
                        queue.Enqueue(child.OpenXmlPart);
                    }
                }
            }
        }

        // Word 2019/365 mirrors comment text into word/threadedComments.xml (plus commentsExtended /
        // commentsIds). We redact the canonical comments part (see EnumerateTargets), so drop the
        // threaded duplicate and its companions to (a) stop the duplicate copy of the text from
        // shipping and (b) leave a clean classic-comments document. The redacted comments part remains
        // (or is deleted by the "remove comments" scrub). No PII lives in the companion parts.
        private static void RemoveThreadedCommentDuplicate(WordprocessingDocument document)
        {
            MainDocumentPart? main = document.MainDocumentPart;
            if (main is null)
            {
                return;
            }

            List<OpenXmlPart> threaded = main.Parts.Select(p => p.OpenXmlPart).Where(IsThreadedCommentsPart).ToList();
            if (threaded.Count == 0)
            {
                return; // nothing threaded to downgrade; leave older comment structures untouched
            }

            foreach (OpenXmlPart part in threaded)
            {
                main.DeletePart(part);
            }
            foreach (WordprocessingCommentsExPart part in main.GetPartsOfType<WordprocessingCommentsExPart>().ToList())
            {
                main.DeletePart(part);
            }
            foreach (WordprocessingCommentsIdsPart part in main.GetPartsOfType<WordprocessingCommentsIdsPart>().ToList())
            {
                main.DeletePart(part);
            }
        }

        private static bool IsThreadedCommentsPart(OpenXmlPart part) =>
            part.ContentType.Contains("threadedcomment", StringComparison.OrdinalIgnoreCase)
            || part.Uri.OriginalString.EndsWith("threadedComments.xml", StringComparison.OrdinalIgnoreCase);

        // Resolves the document's header then footer parts via the section properties' references,
        // ordered First, Even, Default(Odd), de-duplicated (a part may be referenced by many sections).
        private static IEnumerable<OpenXmlPart> OrderedHeaderFooterParts(MainDocumentPart main)
        {
            List<SectionProperties> sectPrs = main.Document?.Body?.Descendants<SectionProperties>().ToList()
                                              ?? new List<SectionProperties>();
            var seen = new HashSet<OpenXmlPart>();

            foreach (HeaderReference reference in sectPrs.SelectMany(s => s.Elements<HeaderReference>()).OrderBy(r => Rank(r.Type)))
            {
                if (reference.Id?.Value is string id && TryGetPart(main, id, out OpenXmlPart part) && seen.Add(part))
                {
                    yield return part;
                }
            }

            foreach (FooterReference reference in sectPrs.SelectMany(s => s.Elements<FooterReference>()).OrderBy(r => Rank(r.Type)))
            {
                if (reference.Id?.Value is string id && TryGetPart(main, id, out OpenXmlPart part) && seen.Add(part))
                {
                    yield return part;
                }
            }
        }

        private static int Rank(EnumValue<HeaderFooterValues>? type)
        {
            if (type is null)
            {
                return 3;
            }
            if (type.Value == HeaderFooterValues.First)
            {
                return 0;
            }
            return type.Value == HeaderFooterValues.Even ? 1 : 2; // Default == Odd
        }

        private static bool TryGetPart(MainDocumentPart main, string id, out OpenXmlPart part)
        {
            try
            {
                part = main.GetPartById(id);
                return true;
            }
            catch
            {
                part = null!;
                return false;
            }
        }

        // The paragraph's own visible text, excluding any text inside a nested text box (which is a
        // separate redaction unit). For a simple paragraph this equals InnerText.
        private static string OwnText(Paragraph paragraph) =>
            string.Concat(OwnTexts(paragraph).Select(t => t.Text));

        private static IEnumerable<Text> OwnTexts(Paragraph paragraph) =>
            paragraph.Descendants<Text>().Where(t => BelongsDirectlyTo(paragraph, t));

        // Runs holding the paragraph's own text (a direct <w:t>), excluding drawing runs and any run
        // inside a nested text box.
        private static List<Run> OwnTextRuns(Paragraph paragraph) =>
            paragraph.Descendants<Run>()
                .Where(r => BelongsDirectlyTo(paragraph, r)
                            && r.Elements<Text>().Any()
                            && !r.Descendants<Drawing>().Any()
                            && !r.Descendants<Picture>().Any())
                .ToList();

        // True when <paramref name="element"/>'s nearest ancestor paragraph is <paramref name="owner"/>
        // — i.e. the element is part of this paragraph's own content, not a nested text-box paragraph.
        // (Ancestors() yields nearest-first, so the first paragraph ancestor is the immediate one.)
        private static bool BelongsDirectlyTo(Paragraph owner, OpenXmlElement element) =>
            ReferenceEquals(element.Ancestors<Paragraph>().FirstOrDefault(), owner);

        // A paragraph that contains a drawing, picture, or text box must not be wiped and rebuilt:
        // doing so would delete the drawing. Such paragraphs are redacted run-by-run instead.
        private static bool ContainsDrawing(Paragraph paragraph) =>
            paragraph.Descendants<Drawing>().Any()
            || paragraph.Descendants<Picture>().Any()
            || paragraph.Descendants<Paragraph>().Any(); // a nested paragraph means text-box content

        private static void ApplyRangesToParagraph(Paragraph paragraph, string ownText, IReadOnlyCollection<ReplacementRange> ranges, bool highlight)
        {
            if (ranges.Count == 0)
            {
                return;
            }
            if (ContainsDrawing(paragraph))
            {
                RebuildComplexParagraph(paragraph, ownText, ranges, highlight);
            }
            else
            {
                RebuildParagraph(paragraph, ownText, ranges, highlight);
            }
        }

        // Empties the paragraph (keeping its properties), then refills it: plain runs for the kept
        // text and a highlighted run for each replacement. Only used for simple paragraphs (no drawing).
        private static void RebuildParagraph(Paragraph paragraph, string original, IEnumerable<ReplacementRange> ranges, bool highlight)
        {
            ParagraphProperties? properties = paragraph.GetFirstChild<ParagraphProperties>();
            paragraph.RemoveAllChildren();
            if (properties is not null)
            {
                paragraph.AppendChild(properties); // must precede the runs
            }
            foreach (Run run in BuildRuns(original, ranges, highlight))
            {
                paragraph.AppendChild(run);
            }
        }

        // Redacts a paragraph that contains a drawing / text box / picture without destroying it: only
        // the paragraph's own text runs are replaced; the drawing and any text-box content are left in
        // place (the text-box's inner paragraphs are redacted as their own units).
        private static void RebuildComplexParagraph(Paragraph paragraph, string ownText, IEnumerable<ReplacementRange> ranges, bool highlight)
        {
            List<Run> ownRuns = OwnTextRuns(paragraph);
            if (ownRuns.Count == 0)
            {
                return; // nothing of the paragraph's own to redact (e.g. a drawing-only paragraph)
            }

            Run anchor = ownRuns[0];
            OpenXmlElement parent = anchor.Parent!;
            foreach (Run run in BuildRuns(ownText, ranges, highlight))
            {
                parent.InsertBefore(run, anchor);
            }
            foreach (Run run in ownRuns)
            {
                run.Remove();
            }
        }

        // Splits text into plain runs for kept spans and a (optionally highlighted) run per replacement.
        private static IEnumerable<Run> BuildRuns(string text, IEnumerable<ReplacementRange> ranges, bool highlight)
        {
            var runs = new List<Run>();
            int last = 0;
            foreach (ReplacementRange range in ranges.OrderBy(r => r.Start))
            {
                if (range.Start < last || range.End > text.Length)
                {
                    continue;
                }
                if (range.Start > last)
                {
                    runs.Add(MakeRun(text.Substring(last, range.Start - last), highlight: false));
                }
                runs.Add(MakeRun(range.Replacement ?? string.Empty, highlight));
                last = range.End;
            }
            if (last < text.Length)
            {
                runs.Add(MakeRun(text.Substring(last), highlight: false));
            }
            return runs;
        }

        private static Run MakeRun(string text, bool highlight)
        {
            var run = new Run();
            if (highlight)
            {
                run.RunProperties = new RunProperties(new Highlight { Val = HighlightColorValues.Yellow });
            }
            // Preserve leading/trailing whitespace so spacing around replacements is kept.
            run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            return run;
        }
    }
}
