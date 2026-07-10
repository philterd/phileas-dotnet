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
using Phileas.Services.Office;
using Phileas.Services;
using Phileas.Model;

using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests
{
    /// <summary>
    /// Tracked-change *deleted* text (&lt;w:delText&gt; inside &lt;w:del&gt;) must never be reintroduced as
    /// normal text when a paragraph is rebuilt (the paragraph view is its &lt;w:t&gt; runs only), and its
    /// PII must still be redacted in place — a separate pass filters the deleted text so it can't ship
    /// verbatim and be recovered with "Reject Changes".
    /// </summary>
    public sealed class WordTrackedChangeRedactionTests : IDisposable
    {
        private static readonly PhileasPolicy EmailPolicy = new()
        {
            Name = "email",
            Identifiers = new Phileas.Policy.Identifiers { EmailAddress = new Phileas.Policy.Filters.EmailAddress() }
        };

        private static Func<string, TextFilterResult> Filter =>
            t => new FilterService().Filter(EmailPolicy, "ctx", 0, t);

        private readonly string _dir;

        public WordTrackedChangeRedactionTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "philter-trk-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        }

        private string NewPath(string name) => Path.Combine(_dir, name);

        // A paragraph: normal text, a tracked deletion, then normal text containing an e-mail.
        private const string ParagraphWithDeletion =
            "<w:p>" +
            "<w:r><w:t xml:space=\"preserve\">Email </w:t></w:r>" +
            "<w:del w:id=\"1\" w:author=\"Reviewer\"><w:r><w:delText xml:space=\"preserve\">DELETEDSECRET </w:delText></w:r></w:del>" +
            "<w:r><w:t xml:space=\"preserve\">to a@example.com today</w:t></w:r>" +
            "</w:p>";

        [Fact]
        public void Redact_RebuiltParagraph_DoesNotReintroduceDeletedText()
        {
            string input = NewPath("in.docx");
            string output = NewPath("out.docx");
            WordDocs.CreateRaw(input, ParagraphWithDeletion);
            Assert.True(WordDocs.AnyPartContains(input, "DELETEDSECRET")); // sanity: present as a deletion

            WordDocumentRedactor.Redact(input, output, Filter);

            // The paragraph was rebuilt (the e-mail changed it); the deleted text must not come back.
            Assert.DoesNotContain(WordDocs.BodyParagraphs(output), p => p.Contains("DELETEDSECRET"));
            Assert.False(WordDocs.AnyPartContains(output, "DELETEDSECRET"));
        }

        [Fact]
        public void Redact_StillRedactsVisibleText_AroundTheDeletion()
        {
            string input = NewPath("in.docx");
            string output = NewPath("out.docx");
            WordDocs.CreateRaw(input, ParagraphWithDeletion);

            WordDocumentRedactor.Redact(input, output, Filter);

            Assert.False(WordDocs.AnyPartContains(output, "@example.com"));
            Assert.Contains(WordDocs.BodyParagraphs(output), p => p.Contains("Email") && p.Contains("REDACTED") && p.Contains("today"));
        }

        [Fact]
        public void ReadParagraphs_ExcludesDeletedText()
        {
            string input = NewPath("in.docx");
            WordDocs.CreateRaw(input, ParagraphWithDeletion);

            List<string> paragraphs = WordDocumentRedactor.ReadParagraphs(input);

            // The redactor's view of the paragraph is its <w:t> text only — no deleted text.
            Assert.Contains(paragraphs, p => p.Contains("Email") && p.Contains("a@example.com") && !p.Contains("DELETEDSECRET"));
        }

        [Fact]
        public void Detect_FlagsPiiInDeletedText()
        {
            // A deletion that itself contains an e-mail is PII that would ship verbatim, so detection
            // (used by the preview and post-redaction verification) must flag it.
            string input = NewPath("in.docx");
            string body =
                "<w:p><w:r><w:t xml:space=\"preserve\">keep </w:t></w:r>" +
                "<w:del w:id=\"1\" w:author=\"R\"><w:r><w:delText xml:space=\"preserve\">deleted@example.com</w:delText></w:r></w:del>" +
                "<w:r><w:t xml:space=\"preserve\"> end</w:t></w:r></w:p>";
            WordDocs.CreateRaw(input, body);

            List<OfficeRedactionSpan> spans = WordDocumentRedactor.Detect(input, Filter);

            Assert.Contains(spans, s => s.Text.Contains("@example.com"));
        }

        [Fact]
        public void Redact_UnchangedParagraphWithDeletion_PreservesItAsTrackedDeletion()
        {
            // A paragraph that is only a tracked deletion (no live PII) isn't rebuilt: the deletion is
            // left exactly as it was (still a <w:delText> deletion, not promoted to normal <w:t> text),
            // while another paragraph's PII is still redacted and the document stays valid.
            string input = NewPath("delonly.docx");
            string output = NewPath("delonly_out.docx");
            string body =
                "<w:p><w:del w:id=\"1\" w:author=\"R\"><w:r><w:delText xml:space=\"preserve\">DELONLY</w:delText></w:r></w:del></w:p>" +
                "<w:p><w:r><w:t xml:space=\"preserve\">body a@example.com</w:t></w:r></w:p>";
            WordDocs.CreateRaw(input, body);

            WordDocumentRedactor.Redact(input, output, Filter);

            string xml = WordDocs.DocumentXml(output);
            Assert.Contains("delText", xml);                       // still a tracked deletion (not promoted)
            Assert.Contains("DELONLY", xml);                       // its text preserved within the deletion
            Assert.False(WordDocs.AnyPartContains(output, "@example.com")); // the other paragraph was redacted
            using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(output, false);
            Assert.NotNull(doc.MainDocumentPart?.Document?.Body);  // valid document
        }
    }
}
