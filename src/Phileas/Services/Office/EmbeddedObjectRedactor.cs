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

using DocumentFormat.OpenXml.Packaging;
using Phileas.Model;

namespace Phileas.Services.Office
{
    /// <summary>
    /// Handles <b>embedded objects</b> (Insert &gt; Object) in Word and Excel files — an
    /// <see cref="EmbeddedPackagePart"/> (an embedded Office document) or an <see cref="EmbeddedObjectPart"/>
    /// (an opaque OLE/binary object). An embedded Word/Excel document carries its own full copy of source
    /// data and is <b>redacted in place</b> by recursing the matching redactor into it. An object we can't
    /// read as an Office document is either <b>removed</b> (when enabled) or kept and flagged, since its
    /// content can't be inspected. Chart-embedded source workbooks are handled separately (they hang off a
    /// chart part and are excluded here).
    /// </summary>
    public static class EmbeddedObjectRedactor
    {
        /// <summary>The heads-up shown when a file still carries an embedded object that couldn't be inspected.</summary>
        public const string KeptCaveat =
            "This file has one or more embedded objects that Philter Desktop could not inspect (for example a " +
            "legacy OLE object or another program's document). Their content was not redacted — review them, " +
            "or turn on \"Remove embedded objects Philter Desktop can't inspect\" in Settings → Microsoft Office.";

        /// <summary>
        /// Recurses into embedded Office documents (redacting them) and removes/flags opaque objects. Runs only
        /// at the top level of a redaction, so an embedded document's own embedded objects aren't re-descended.
        /// </summary>
        public static void Process(
            OpenXmlPartContainer root, Func<string, TextFilterResult> filter, bool removeUninspectable,
            bool write, List<OfficeRedactionSpan>? captured, ref int order)
        {
            foreach ((OpenXmlPartContainer parent, OpenXmlPart part) in FindEmbeddedObjects(root))
            {
                Handle(parent, part, filter, removeUninspectable, write, captured, ref order);
            }
        }

        /// <summary>True if the file still carries an embedded object Philter Desktop can't inspect.</summary>
        public static bool DocxHasUninspectable(string path)
        {
            try
            {
                using WordprocessingDocument doc = WordprocessingDocument.Open(path, isEditable: false);
                return HasUninspectable(doc);
            }
            catch { return false; }
        }

        /// <summary>True if the file still carries an embedded object Philter Desktop can't inspect.</summary>
        public static bool XlsxHasUninspectable(string path)
        {
            try
            {
                using SpreadsheetDocument doc = SpreadsheetDocument.Open(path, isEditable: false);
                return HasUninspectable(doc);
            }
            catch { return false; }
        }

        private static bool HasUninspectable(OpenXmlPartContainer root) =>
            FindEmbeddedObjects(root).Any(x => IsOpaque(x.Part));

        // Breadth-first over every part reachable from root, collecting embedded objects whose parent is not a
        // chart part (chart-embedded workbooks are handled by the chart pass).
        private static List<(OpenXmlPartContainer Parent, OpenXmlPart Part)> FindEmbeddedObjects(OpenXmlPartContainer root)
        {
            var found = new List<(OpenXmlPartContainer, OpenXmlPart)>();
            var seen = new HashSet<OpenXmlPart>();
            var queue = new Queue<OpenXmlPartContainer>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                OpenXmlPartContainer container = queue.Dequeue();
                foreach (IdPartPair pair in container.Parts)
                {
                    OpenXmlPart part = pair.OpenXmlPart;
                    if (!seen.Add(part))
                    {
                        continue;
                    }
                    if (container is not ChartPart && (part is EmbeddedPackagePart || part is EmbeddedObjectPart))
                    {
                        found.Add((container, part));
                    }
                    queue.Enqueue(part);
                }
            }
            return found;
        }

        // An EmbeddedObjectPart is always an opaque OLE object; an EmbeddedPackagePart is opaque unless it's an
        // embedded Word or Excel document (which we can open and redact).
        private static bool IsOpaque(OpenXmlPart part) =>
            part is not EmbeddedPackagePart pkg ||
            !(pkg.ContentType.Contains("spreadsheetml", StringComparison.OrdinalIgnoreCase) ||
              pkg.ContentType.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase));

        private static void Handle(
            OpenXmlPartContainer parent, OpenXmlPart part, Func<string, TextFilterResult> filter,
            bool removeUninspectable, bool write, List<OfficeRedactionSpan>? captured, ref int order)
        {
            byte[] bytes;
            try
            {
                using Stream s = part.GetStream(FileMode.Open, FileAccess.Read);
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                bytes = ms.ToArray();
            }
            catch { return; }
            if (bytes.Length == 0)
            {
                return;
            }

            bool isXlsx = part is EmbeddedPackagePart px && px.ContentType.Contains("spreadsheetml", StringComparison.OrdinalIgnoreCase);
            bool isDocx = part is EmbeddedPackagePart pw && pw.ContentType.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase);

            if (!write)
            {
                if (isXlsx)
                {
                    AddSpans(XlsxRedactor.DetectEmbeddedBytes(bytes, filter), captured, ref order);
                }
                else if (isDocx)
                {
                    AddSpans(WordDocumentRedactor.DetectEmbeddedBytes(bytes, filter), captured, ref order);
                }
                return; // opaque objects can't be scanned for detection
            }

            byte[]? redacted = null;
            List<OfficeRedactionSpan> embSpans = new();
            if (isXlsx)
            {
                redacted = XlsxRedactor.RedactEmbeddedBytes(bytes, filter, out embSpans);
            }
            else if (isDocx)
            {
                redacted = WordDocumentRedactor.RedactEmbeddedBytes(bytes, filter, out embSpans);
            }

            if (redacted is not null)
            {
                try
                {
                    using var src = new MemoryStream(redacted);
                    part.FeedData(src);
                }
                catch { return; }
                AddSpans(embSpans, captured, ref order);
                return;
            }

            // Opaque (or an embedded document that failed to parse): can't inspect its content.
            if (removeUninspectable)
            {
                try
                {
                    parent.DeletePart(part);
                    captured?.Add(Marker(order++, "[embedded object]", "[removed]", "embedded-object-removed"));
                }
                catch { /* best effort */ }
            }
            else
            {
                captured?.Add(Marker(order++, "[embedded object]", "[kept — not inspected]", "embedded-object-kept"));
            }
        }

        private static void AddSpans(List<OfficeRedactionSpan> spans, List<OfficeRedactionSpan>? captured, ref int order)
        {
            if (captured is null)
            {
                return;
            }
            foreach (OfficeRedactionSpan span in spans)
            {
                span.Order = order++;
                span.ParagraphIndex = -1; // embedded content, not a paragraph offset in the outer document
                captured.Add(span);
            }
        }

        private static OfficeRedactionSpan Marker(int order, string text, string replacement, string classification) => new()
        {
            Order = order,
            ParagraphIndex = -1,
            Text = text,
            Replacement = replacement,
            Classification = classification
        };
    }
}
