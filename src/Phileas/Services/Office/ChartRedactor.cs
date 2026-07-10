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

        // Concatenates a DrawingML paragraph's runs, filters, and (when changed) flattens the result into
        // the first run — the same approach the Word body/drawing rebuild uses; the run/chart structure is
        // preserved.
        private static void RedactDrawingParagraph(A.Paragraph paragraph, Func<string, TextFilterResult> filter,
            bool write, List<OfficeRedactionSpan>? captured, ref int order)
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
            if (write)
            {
                texts[0].Text = result.FilteredText;
                for (int i = 1; i < texts.Count; i++)
                {
                    texts[i].Text = string.Empty;
                }
            }
            Capture(result, original, captured, ref order);
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
