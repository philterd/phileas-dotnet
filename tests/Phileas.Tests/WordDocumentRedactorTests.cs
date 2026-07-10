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

using System.IO.Compression;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests
{
    /// <summary>
    /// Tests Word (.docx) redaction via the Open XML SDK implementation. No license required, so
    /// these run unconditionally.
    /// </summary>
    public sealed class WordDocumentRedactorTests : IDisposable
    {
        private static readonly PhileasPolicy EmailPolicy = new()
        {
            Name = "email",
            Identifiers = new Identifiers { EmailAddress = new EmailAddress() }
        };

        // Real filter producing spans; emails redact to {{{REDACTED-email-address}}}.
        private static Func<string, TextFilterResult> Filter =>
            t => new FilterService().Filter(EmailPolicy, "ctx", 0, t);

        private readonly string _tempDir;

        public WordDocumentRedactorTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "philter-word-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        }

        private string NewPath(string name) => Path.Combine(_tempDir, name);

        private string CreateDocx(params string[] paragraphs)
        {
            string path = NewPath("in_" + Guid.NewGuid().ToString("N") + ".docx");
            WordDocs.Create(path, paragraphs);
            return path;
        }

        // Regression test for the bug we hit: emptying a changed paragraph must not delete the
        // paragraph itself, so the rebuild is preserved and the paragraph count is unchanged.
        [Fact]
        public void Redact_ReplacesChangedParagraphs_WithoutDeletingThem()
        {
            string input = CreateDocx("keep one", "email aaa@example.com here", "email bbb@example.com too", "keep four");
            int originalCount = WordDocs.BodyParagraphs(input).Length;
            string output = NewPath("out.docx");

            WordDocumentRedactor.Redact(input, output, Filter);

            string[] result = WordDocs.BodyParagraphs(output);
            Assert.Equal(originalCount, result.Length); // no paragraph deleted
            Assert.Contains("keep one", result);
            Assert.Contains("keep four", result);
            Assert.Contains("email {{{REDACTED-email-address}}} here", result);
            Assert.DoesNotContain(result, p => p.Contains("@example.com"));
        }

        [Fact]
        public void ReadParagraphs_And_Detect_GiveParagraphIndexedSpans()
        {
            string input = CreateDocx("keep one", "email aaa@example.com here", "keep three");

            string[] paragraphs = WordDocumentRedactor.ReadParagraphs(input).ToArray();
            Assert.Equal(new[] { "keep one", "email aaa@example.com here", "keep three" }, paragraphs);

            List<OfficeRedactionSpan> spans = WordDocumentRedactor.Detect(input, Filter);
            OfficeRedactionSpan span = Assert.Single(spans);
            Assert.Equal(1, span.ParagraphIndex);        // second paragraph
            Assert.Equal("aaa@example.com", span.Text);
            // The input file is read-only/untouched by detection.
            Assert.Contains(WordDocs.BodyParagraphs(input), p => p.Contains("@example.com"));
        }

        [Fact]
        public void Redact_UnchangedText_LeavesParagraphsIdentical()
        {
            string input = CreateDocx("alpha", "beta", "gamma"); // no PII
            string output = NewPath("out.docx");

            WordDocumentRedactor.Redact(input, output, Filter);

            Assert.Equal(WordDocs.BodyParagraphs(input), WordDocs.BodyParagraphs(output));
        }

        [Fact]
        public void Redact_RedactsHeadersAndFooters()
        {
            string input = NewPath("hf.docx");
            WordDocs.CreateWithHeaderFooter(input, "header hh@example.com", "footer ff@example.com", "body bb@example.com");
            string output = NewPath("hf_out.docx");

            WordDocumentRedactor.Redact(input, output, Filter);

            string body = WordDocs.AllBodyText(output);
            string header = WordDocs.HeadersText(output);
            string footer = WordDocs.FootersText(output);

            Assert.DoesNotContain("@example.com", body + header + footer);
            Assert.Contains("REDACTED", header);
            Assert.Contains("REDACTED", footer);
            Assert.Contains("REDACTED", body);
        }

        [Fact]
        public void Redact_LeavesInputFileUnchanged()
        {
            string input = CreateDocx("contact data@example.com");
            string output = NewPath("out.docx");

            WordDocumentRedactor.Redact(input, output, Filter);

            Assert.Contains(WordDocs.BodyParagraphs(input), p => p.Contains("@example.com"));
        }

        // a failure mid-redaction must never leave the original (unredacted) — or a partial —
        // file at the output path. With the in-memory build, the output is written only on full success,
        // so a throwing filter leaves no output file at all.
        [Fact]
        public void Redact_FailureMidway_LeavesNoOutputFile()
        {
            string input = CreateDocx("keep one", "boom secret@example.com here");
            string output = NewPath("out.docx");

            Func<string, TextFilterResult> throwingFilter = t =>
                t.Contains("boom") ? throw new InvalidOperationException("simulated redaction failure") : Filter(t);

            Assert.Throws<InvalidOperationException>(() => WordDocumentRedactor.Redact(input, output, throwingFilter));
            Assert.False(File.Exists(output), "no output file should be left when redaction fails");
        }

        [Fact]
        public void Redact_WithHighlight_HighlightsTheReplacement()
        {
            string input = CreateDocx("email zzz@example.com end");
            string output = NewPath("out.docx");

            WordDocumentRedactor.Redact(input, output, Filter, highlight: true);

            Assert.Contains("email {{{REDACTED-email-address}}} end", WordDocs.BodyParagraphs(output));

            // The replacement run carries Word's native yellow highlight.
            string xml = ReadDocumentXml(output);
            Assert.Contains("highlight", xml);
            Assert.Contains("yellow", xml);
        }

        // --- Tracked deletions (w:delText) -----------------------------------

        [Fact]
        public void Redact_RedactsDeletedTrackedText_KeepsTheDeletion()
        {
            // A tracked deletion carrying PII: its text lives in w:delText, not w:t.
            string input = NewPath("del-in.docx");
            WordDocs.CreateWithTrackedDeletion(input, "removed contact aaa@example.com", "Visible body text.");
            string output = NewPath("del-out.docx");

            WordDocumentRedactor.Redact(input, output, Filter);

            string[] deleted = WordDocs.DeletedTexts(output);
            Assert.NotEmpty(deleted); // the deletion is preserved (not dropped)
            string joined = string.Concat(deleted);
            Assert.DoesNotContain("aaa@example.com", joined); // its PII is redacted in place
            Assert.Contains("REDACTED", joined);
            // And nothing anywhere in the package still holds the address (strong leak check).
            Assert.False(WordDocs.AnyPartContains(output, "aaa@example.com"));
        }

        [Fact]
        public void Redact_ReportsDeletedTextRedactionAsSpan()
        {
            string input = NewPath("del-span-in.docx");
            WordDocs.CreateWithTrackedDeletion(input, "old email aaa@example.com", "Body.");
            string output = NewPath("del-span-out.docx");

            List<OfficeRedactionSpan> spans = WordDocumentRedactor.Redact(input, output, Filter);

            Assert.Contains(spans, s => s.Text.Contains("aaa@example.com"));
        }

        [Fact]
        public void Detect_FindsPiiInDeletedText()
        {
            string input = NewPath("del-detect.docx");
            WordDocs.CreateWithTrackedDeletion(input, "hidden aaa@example.com", "Body.");

            List<OfficeRedactionSpan> residuals = WordDocumentRedactor.Detect(input, Filter);

            Assert.Contains(residuals, s => s.Text.Contains("aaa@example.com"));
        }

        [Fact]
        public void ApplySpans_WithFilter_RedactsDeletedText_LikeModify()
        {
            // Modify Redaction re-renders from the original source and passes a policy filter for the
            // non-positional passes (drawings/hyperlinks/deletions). The deleted text must be redacted.
            string input = NewPath("del-apply-in.docx");
            WordDocs.CreateWithTrackedDeletion(input, "old aaa@example.com", "Body.");
            string output = NewPath("del-apply-out.docx");

            WordDocumentRedactor.ApplySpans(input, output, new List<OfficeRedactionSpan>(), highlight: false, drawingFilter: Filter);

            Assert.False(WordDocs.AnyPartContains(output, "aaa@example.com"));
        }

        private static string ReadDocumentXml(string docxPath)
        {
            using ZipArchive zip = ZipFile.OpenRead(docxPath);
            ZipArchiveEntry entry = zip.GetEntry("word/document.xml")!;
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        }
    }
}
