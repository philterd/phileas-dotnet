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
using Phileas.Model;
using A = DocumentFormat.OpenXml.Drawing;

namespace Phileas.Services.Office
{
    /// <summary>
    /// Redacts detected PII inside an embedded chart part — shared by the Word and Excel redactors, since a
    /// chart is the same DrawingML <c>&lt;c:chartSpace&gt;</c> in both. Two things carry text: the chart's
    /// DrawingML title/axis/data-label rich text (<c>&lt;a:t&gt;</c>), and the <b>cached</b> series,
    /// category, and series-name values (<c>&lt;c:v&gt;</c> inside <c>numCache</c>/<c>strCache</c>) that
    /// copy the source cells' values and would otherwise ship verbatim. Both are run through the policy
    /// filter. Redactions are captured with <see cref="OfficeRedactionSpan.ParagraphIndex"/> -1 (not a body
    /// paragraph). With <c>write</c> false it only detects (for the preview / verification).
    /// </summary>
    public static class ChartRedactor
    {
        private const string ChartNamespace = "http://schemas.openxmlformats.org/drawingml/2006/chart";

        /// <summary>
        /// Redacts (<paramref name="write"/> true) or detects (<paramref name="write"/> false) PII in one
        /// chart part: its DrawingML title/axis/label text and the cached series/category values.
        /// </summary>
        public static void RedactChartPart(OpenXmlPart chartPart, Func<string, TextFilterResult> filter,
            bool write, List<OfficeRedactionSpan>? captured, ref int order)
        {
            OpenXmlElement? root;
            try
            {
                root = chartPart.RootElement; // parses the part; skip parts the SDK can't type
            }
            catch
            {
                return;
            }
            if (root is null)
            {
                return;
            }

            // Title, axis titles, and data-label rich text.
            RedactDrawingText(root, filter, write, captured, ref order);

            // Cached series/category values and cached series names (<c:v>). Cell references (<c:f>) and
            // numeric formats are left alone.
            foreach (OpenXmlLeafTextElement value in root.Descendants<OpenXmlLeafTextElement>()
                         .Where(e => e.LocalName == "v" && e.NamespaceUri == ChartNamespace).ToList())
            {
                RedactLeaf(value, filter, write, captured, ref order);
            }
        }

        /// <summary>
        /// Redacts every DrawingML paragraph (<c>&lt;a:p&gt;</c>) under <paramref name="root"/> — the shared
        /// text path for charts (title/axis/labels) and worksheet shapes/text boxes, since both store text as
        /// <c>&lt;a:t&gt;</c> runs. With <paramref name="write"/> false it only detects.
        /// </summary>
        public static void RedactDrawingText(OpenXmlElement root, Func<string, TextFilterResult> filter,
            bool write, List<OfficeRedactionSpan>? captured, ref int order)
        {
            foreach (A.Paragraph paragraph in root.Descendants<A.Paragraph>().ToList())
            {
                RedactDrawingParagraph(paragraph, filter, write, captured, ref order);
            }
        }

        // Filters a DrawingML paragraph's text and, when it changed, writes the result back run by run.
        // Each line break (<a:br>) is a "\n" in the filtered text, so the words on either side are not
        // joined, and the break is left where it is. Pouring the whole result into the first run, as this
        // used to, put every break after all of the text. See philterd/phileas-dotnet#151.
        private static void RedactDrawingParagraph(A.Paragraph paragraph, Func<string, TextFilterResult> filter,
            bool write, List<OfficeRedactionSpan>? captured, ref int order)
        {
            List<(OpenXmlElement Element, string Text)> content = DrawingContent(paragraph);
            if (!content.Any(c => c.Element is A.Text))
            {
                return;
            }
            string original = string.Concat(content.Select(c => c.Text));
            if (string.IsNullOrEmpty(original))
            {
                return;
            }
            TextFilterResult result = filter(original);
            if (string.Equals(result.FilteredText, original, StringComparison.Ordinal))
            {
                return;
            }
            if (write)
            {
                List<ReplacementRange> ranges = OfficeSpanMath.ResolveNonOverlapping(result.Spans
                    .Where(s => s.CharacterStart >= 0 && s.CharacterEnd <= original.Length && s.CharacterEnd > s.CharacterStart)
                    .Select(s => new ReplacementRange(s.CharacterStart, s.CharacterEnd, s.Replacement ?? string.Empty)));
                if (!Rewrite(content, original, ranges, result.FilteredText))
                {
                    // The spans don't account for the filtered text, as a hand-written filter delegate's
                    // may not. Write the filtered text whole rather than risk leaving PII in place: the
                    // breaks move to the end, but nothing the filter removed survives. Its "\n"s become
                    // spaces, since the <a:br> elements they stand for are still in the paragraph.
                    List<A.Text> texts = content.Select(c => c.Element).OfType<A.Text>().ToList();
                    texts[0].Text = result.FilteredText.Replace("\n", " ");
                    for (int i = 1; i < texts.Count; i++)
                    {
                        texts[i].Text = string.Empty;
                    }
                }
            }
            Capture(result, original, captured, ref order);
        }

        /// <summary>
        /// The text of a DrawingML paragraph as the filter sees it: its <c>&lt;a:t&gt;</c> text, with
        /// <c>"\n"</c> for each line break (<c>&lt;a:br&gt;</c>). Used for the review diff so it shows the
        /// same text that was redacted.
        /// </summary>
        internal static string DrawingParagraphText(A.Paragraph paragraph) =>
            string.Concat(DrawingContent(paragraph).Select(c => c.Text));

        // A DrawingML paragraph's text elements and line breaks in document order, each with the text it
        // contributes. A line break is one character, so its offset maps back to it.
        private static List<(OpenXmlElement Element, string Text)> DrawingContent(A.Paragraph paragraph)
        {
            var content = new List<(OpenXmlElement, string)>();
            foreach (OpenXmlElement element in paragraph.Descendants())
            {
                if (element is A.Text text)
                {
                    content.Add((text, text.Text ?? string.Empty));
                }
                else if (element is A.Break)
                {
                    content.Add((element, "\n"));
                }
            }
            return content;
        }

        // Applies the replacements to each text element's own slice of the paragraph, leaving the line
        // breaks untouched. A replacement is written where its span starts, at the first character of the
        // span that is not a line break; the rest of the span's characters are removed from whichever runs
        // hold them. Each run keeps its own formatting and each break stays between the same text.
        // Returns false, changing nothing, when the replacements do not produce
        // <paramref name="filteredText"/>.
        private static bool Rewrite(List<(OpenXmlElement Element, string Text)> content, string original,
            IReadOnlyList<ReplacementRange> ranges, string filteredText)
        {
            var rangeAt = new int[original.Length];
            Array.Fill(rangeAt, -1);
            for (int r = 0; r < ranges.Count; r++)
            {
                for (int i = ranges[r].Start; i < ranges[r].End; i++)
                {
                    rangeAt[i] = r;
                }
            }

            var written = new bool[ranges.Count];
            var rewrites = new List<(A.Text Element, string Text)>();
            var produced = new System.Text.StringBuilder(filteredText.Length);
            int offset = 0;
            foreach ((OpenXmlElement element, string text) in content)
            {
                if (element is not A.Text textElement)
                {
                    // A break inside a replaced span is kept in the paragraph but is not in the filtered
                    // text, which replaced it along with the rest of the span.
                    if (rangeAt[offset] < 0)
                    {
                        produced.Append(text);
                    }
                }
                else
                {
                    var rewritten = new System.Text.StringBuilder(text.Length);
                    for (int i = offset; i < offset + text.Length; i++)
                    {
                        int r = rangeAt[i];
                        if (r < 0)
                        {
                            rewritten.Append(original[i]);
                        }
                        else if (!written[r])
                        {
                            rewritten.Append(ranges[r].Replacement);
                            written[r] = true;
                        }
                    }
                    rewrites.Add((textElement, rewritten.ToString()));
                    produced.Append(rewritten);
                }
                offset += text.Length;
            }

            if (!string.Equals(produced.ToString(), filteredText, StringComparison.Ordinal))
            {
                return false;
            }
            foreach ((A.Text element, string text) in rewrites)
            {
                element.Text = text;
            }
            return true;
        }

        private static void RedactLeaf(OpenXmlLeafTextElement element, Func<string, TextFilterResult> filter,
            bool write, List<OfficeRedactionSpan>? captured, ref int order)
        {
            string original = element.Text ?? string.Empty;
            if (string.IsNullOrEmpty(original))
            {
                return;
            }
            TextFilterResult result = filter(original);
            if (string.Equals(result.FilteredText, original, StringComparison.Ordinal))
            {
                return;
            }
            if (write)
            {
                element.Text = result.FilteredText;
            }
            Capture(result, original, captured, ref order);
        }

        private static void Capture(TextFilterResult result, string original, List<OfficeRedactionSpan>? captured, ref int order)
        {
            if (captured is null)
            {
                return;
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
}
