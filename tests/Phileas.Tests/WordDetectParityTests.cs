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
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Phileas.Model;
using Phileas.Policy.Filters;
using Phileas.Services;
using Phileas.Services.Office;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;
using PolicyIdentifiers = Phileas.Policy.Identifiers;

namespace Phileas.Tests
{
    /// <summary>
    /// <see cref="WordDocumentRedactor.Detect(string, Func{string, TextFilterResult}, bool, bool)"/> reports
    /// what <c>Redact</c> redacts, including shape/SmartArt text and hyperlink targets, without modifying
    /// the document. See philterd/phileas-dotnet#153.
    /// </summary>
    public sealed class WordDetectParityTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "phileas-word-detect-" + Guid.NewGuid().ToString("N"));

        private static readonly PhileasPolicy EmailPolicy = new()
        {
            Name = "email",
            Identifiers = new PolicyIdentifiers { EmailAddress = new EmailAddress() }
        };

        private static readonly PhileasPolicy EmailAndUrlPolicy = new()
        {
            Name = "email-url",
            Identifiers = new PolicyIdentifiers { EmailAddress = new EmailAddress(), Url = new Url() }
        };

        public WordDetectParityTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        }

        private string NewPath() => Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".docx");

        private static Func<string, TextFilterResult> FilterWith(PhileasPolicy policy)
        {
            var service = new FilterService();
            return text => service.Filter(policy, "ctx", 0, text);
        }

        private static string Describe(OfficeRedactionSpan s) =>
            $"{s.Order}|{s.ParagraphIndex}|{s.CharacterStart}-{s.CharacterEnd}|{s.Text}|{s.Replacement}|{s.Classification}";

        private static void WritePart(OpenXmlPart part, string xml)
        {
            using Stream stream = part.GetStream(FileMode.Create, FileAccess.Write);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(xml);
        }

        // A document with PII in body text, a SmartArt node, a chart title, a hyperlink target and a tracked
        // deletion, so every pass Redact runs has something to report.
        private string CreateDocumentWithEveryLocation()
        {
            string path = NewPath();
            WordDocs.CreateWithHyperlink(path, "Write to us", "mailto:jane@example.com", "Body mail body@example.com here.");
            using WordprocessingDocument doc = WordprocessingDocument.Open(path, isEditable: true);
            MainDocumentPart main = doc.MainDocumentPart!;

            WritePart(main.AddNewPart<DiagramDataPart>(),
                "<dgm:dataModel xmlns:dgm=\"http://schemas.openxmlformats.org/drawingml/2006/diagram\" " +
                "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">" +
                "<dgm:ptLst><dgm:pt dgm:modelId=\"1\"><dgm:t><a:bodyPr/><a:lstStyle/>" +
                "<a:p><a:r><a:t>Node smart@example.com</a:t></a:r></a:p></dgm:t></dgm:pt></dgm:ptLst></dgm:dataModel>");
            WritePart(main.AddNewPart<ChartPart>(),
                "<c:chartSpace xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\" " +
                "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">" +
                "<c:chart><c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/>" +
                "<a:p><a:r><a:t>Chart chart@example.com</a:t></a:r></a:p></c:rich></c:tx></c:title>" +
                "<c:plotArea><c:layout/></c:plotArea></c:chart></c:chartSpace>");
            main.Document!.Body!.AppendChild(new Paragraph(new DeletedRun(new Run(new DeletedText("gone@example.com")))
            {
                Author = "Reviewer", Id = "1"
            }));
            return path;
        }

        [Fact]
        public void Detect_ReportsSmartArtText_WithoutModifyingTheDocument()
        {
            string input = NewPath();
            WordDocs.CreateWithSmartArt(input, WordDocs.AParagraph("Mail john@example.com"), "Body.");
            byte[] before = File.ReadAllBytes(input);

            List<OfficeRedactionSpan> spans = WordDocumentRedactor.Detect(input, FilterWith(EmailPolicy));

            OfficeRedactionSpan span = Assert.Single(spans);
            Assert.Equal("john@example.com", span.Text);
            Assert.Equal(-1, span.ParagraphIndex);
            Assert.Equal(before, File.ReadAllBytes(input));
            Assert.Contains("john@example.com", WordDocs.AllDrawingText(input));
        }

        [Fact]
        public void Detect_ReportsHyperlinkTargets_WithoutModifyingTheDocument()
        {
            string input = NewPath();
            WordDocs.CreateWithHyperlink(input, "Write to us", "mailto:jane@example.com", "Body.");
            byte[] before = File.ReadAllBytes(input);

            List<OfficeRedactionSpan> spans = WordDocumentRedactor.Detect(input, FilterWith(EmailPolicy));

            OfficeRedactionSpan span = Assert.Single(spans);
            Assert.Equal("mailto:jane@example.com", span.Text);
            Assert.Equal(-1, span.ParagraphIndex);
            Assert.Equal(before, File.ReadAllBytes(input));
            Assert.Equal(new[] { "mailto:jane@example.com" }, WordDocs.HyperlinkTargets(input));
        }

        [Fact]
        public void Detect_ReportsHyperlinkTargetsInAHeader()
        {
            string input = NewPath();
            WordDocs.CreateWithHyperlinkInHeader(input, "Write to us", "mailto:jane@example.com", "Body.");

            List<OfficeRedactionSpan> spans = WordDocumentRedactor.Detect(input, FilterWith(EmailPolicy));

            Assert.Equal("mailto:jane@example.com", Assert.Single(spans).Text);
        }

        [Fact]
        public void Detect_ReturnsTheSameSpansAsRedact()
        {
            string input = CreateDocumentWithEveryLocation();

            List<OfficeRedactionSpan> detected = WordDocumentRedactor.Detect(input, FilterWith(EmailPolicy));
            List<OfficeRedactionSpan> redacted = WordDocumentRedactor.Redact(input, NewPath(), FilterWith(EmailPolicy));

            Assert.Equal(redacted.Select(Describe), detected.Select(Describe));
            Assert.Equal(
                new[] { "body@example.com", "smart@example.com", "mailto:jane@example.com", "gone@example.com", "chart@example.com" },
                detected.Select(s => s.Text));
        }

        [Fact]
        public void DetectOnARedactedFile_ReportsNothing_EvenWithAUrlFilter()
        {
            // The redacted hyperlink target is a URL. Reporting it would make verifying the output say PII
            // was left behind.
            string input = CreateDocumentWithEveryLocation();
            string output = NewPath();
            WordDocumentRedactor.Redact(input, output, FilterWith(EmailAndUrlPolicy));

            Assert.Equal(new[] { "https://redacted.invalid/" }, WordDocs.HyperlinkTargets(output));
            Assert.Empty(WordDocumentRedactor.Detect(output, FilterWith(EmailAndUrlPolicy)));
        }

        [Fact]
        public void RedactingARedactedFileAgain_DoesNotReportTheHyperlinkPlaceholder()
        {
            string input = NewPath();
            WordDocs.CreateWithHyperlink(input, "Write to us", "mailto:jane@example.com", "Body.");
            string once = NewPath();
            string twice = NewPath();

            WordDocumentRedactor.Redact(input, once, FilterWith(EmailAndUrlPolicy));
            List<OfficeRedactionSpan> second = WordDocumentRedactor.Redact(once, twice, FilterWith(EmailAndUrlPolicy));

            Assert.Empty(second);
            Assert.True(WordDocs.HyperlinkIdsAllResolve(twice));
        }
    }
}
