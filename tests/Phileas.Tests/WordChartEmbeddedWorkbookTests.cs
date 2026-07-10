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
    /// A native chart in a .docx keeps its authoritative source workbook as an embedded package (recoverable
    /// via Edit Data in Excel). It holds more than the plotted cache — an unplotted PII column ships verbatim.
    /// These pin that the redactor recurses into the embedded workbook, detects/modifies it too, and that an
    /// unparseable embedded package is stripped as a fallback.
    /// </summary>
    public sealed class WordChartEmbeddedWorkbookTests : IDisposable
    {
        private static readonly PhileasPolicy EmailPolicy = new()
        {
            Name = "email",
            Identifiers = new Phileas.Policy.Identifiers { EmailAddress = new Phileas.Policy.Filters.EmailAddress() }
        };

        private static Func<string, TextFilterResult> EmailFilter =>
            t => new FilterService().Filter(EmailPolicy, "ctx", 0, t);

        private readonly string _dir;
        private const string Email = "george@fake.com";

        public WordChartEmbeddedWorkbookTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "philter-embwb-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best effort */ } }

        private string NewPath(string name) => Path.Combine(_dir, name);

        // An embedded workbook whose column C (Contact) holds PII that the chart does NOT plot.
        private byte[] EmbeddedWorkbookWithUnplottedPii()
        {
            string tmp = NewPath("emb-src.xlsx");
            SpreadsheetTestHelper.CreateXlsx(tmp, new List<string?[]>
            {
                new string?[] { "Region", "Sales", "Contact" },
                new string?[] { "North", "42", Email }
            });
            return File.ReadAllBytes(tmp);
        }

        [Fact]
        public void Redact_EmbeddedWorkbook_UnplottedPiiColumn_IsRedacted()
        {
            string input = NewPath("chart.docx");
            string output = NewPath("chart_out.docx");
            WordDocs.CreateWithChartEmbeddedWorkbook(input, EmbeddedWorkbookWithUnplottedPii(), "Sales", "North");
            Assert.Contains(Email, WordDocs.EmbeddedChartWorkbookAllText(input)); // present before

            WordDocumentRedactor.Redact(input, output, EmailFilter);

            Assert.True(WordDocs.HasChartEmbeddedWorkbook(output), "a parseable workbook is redacted in place, not removed");
            Assert.DoesNotContain(Email, WordDocs.EmbeddedChartWorkbookAllText(output));
        }

        [Fact]
        public void Detect_FindsPiiInEmbeddedWorkbook()
        {
            string input = NewPath("detect.docx");
            WordDocs.CreateWithChartEmbeddedWorkbook(input, EmbeddedWorkbookWithUnplottedPii(), "Sales", "North");

            List<OfficeRedactionSpan> spans = WordDocumentRedactor.Detect(input, EmailFilter);

            Assert.Contains(spans, s => s.Text.Contains(Email));
        }

        [Fact]
        public void ApplySpans_WithDrawingFilter_RedactsEmbeddedWorkbook()
        {
            string input = NewPath("apply.docx");
            string output = NewPath("apply_out.docx");
            WordDocs.CreateWithChartEmbeddedWorkbook(input, EmbeddedWorkbookWithUnplottedPii(), "Sales", "North");

            List<OfficeRedactionSpan> spans = WordDocumentRedactor.Detect(input, EmailFilter);
            WordDocumentRedactor.ApplySpans(input, output, spans, highlight: false, drawingFilter: EmailFilter);

            Assert.DoesNotContain(Email, WordDocs.EmbeddedChartWorkbookAllText(output));
        }

        [Fact]
        public void Redact_UnparseableEmbeddedPackage_IsStripped()
        {
            // An embedded package that isn't a valid workbook can't be redacted -> the fallback removes it.
            string input = NewPath("bogus.docx");
            string output = NewPath("bogus_out.docx");
            byte[] garbage = System.Text.Encoding.UTF8.GetBytes("not a real xlsx " + Email);
            WordDocs.CreateWithChartEmbeddedWorkbook(input, garbage, "Sales", "North");

            WordDocumentRedactor.Redact(input, output, EmailFilter);

            Assert.False(WordDocs.HasChartEmbeddedWorkbook(output), "an unredactable embedded package must be removed");
            Assert.False(WordDocs.AnyPartContains(output, Email));
        }
    }
}
