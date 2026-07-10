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

using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Phileas.Services.Office;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests
{
    /// <summary>
    /// Tests the <c>byte[]</c> (in-memory) overloads of the Word and Excel redactors: that they redact,
    /// return spans, round-trip through <c>ApplySpans</c>, leave the input array untouched, and produce the
    /// same result as the file-path overloads. These enable a headless service to redact document bytes
    /// without touching the file system.
    /// </summary>
    public sealed class OfficeByteOverloadTests : IDisposable
    {
        private readonly string _dir;

        public OfficeByteOverloadTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "philter-office-bytes-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        }

        private string Path_(string name) => Path.Combine(_dir, name);

        private static readonly PhileasPolicy Policy = new()
        {
            Name = "email-ssn",
            Identifiers = new Identifiers { EmailAddress = new EmailAddress(), Ssn = new Ssn() }
        };

        private static Func<string, TextFilterResult> Filter()
        {
            var fs = new FilterService();
            return text => fs.Filter(Policy, "ctx", 0, text);
        }

        // --- Word -----------------------------------------------------------------------------------

        [Fact]
        public void Word_Redact_Bytes_RedactsAndReturnsSpans()
        {
            string input = Path_("in.docx");
            WordDocs.Create(input, "keep this", "email alice@example.com here");
            byte[] inputBytes = File.ReadAllBytes(input);

            (byte[] document, List<OfficeRedactionSpan> spans) = WordDocumentRedactor.Redact(inputBytes, Filter());

            Assert.NotEmpty(spans);
            Assert.Contains(spans, s => s.Text == "alice@example.com");

            string output = Path_("out.docx");
            File.WriteAllBytes(output, document);
            Assert.DoesNotContain("alice@example.com", WordDocs.AllBodyText(output));
            Assert.Contains("keep this", WordDocs.AllBodyText(output));
        }

        [Fact]
        public void Word_Redact_Bytes_DoesNotMutateInputArray()
        {
            string input = Path_("in.docx");
            WordDocs.Create(input, "email alice@example.com here");
            byte[] inputBytes = File.ReadAllBytes(input);
            byte[] snapshot = (byte[])inputBytes.Clone();

            WordDocumentRedactor.Redact(inputBytes, Filter());

            Assert.Equal(snapshot, inputBytes); // the caller's buffer is untouched
        }

        [Fact]
        public void Word_Redact_Bytes_MatchesPathOverload()
        {
            string input = Path_("in.docx");
            WordDocs.Create(input, "keep", "email bob@example.com and ssn 123-45-6789");

            string pathOut = Path_("path.docx");
            List<OfficeRedactionSpan> pathSpans = WordDocumentRedactor.Redact(input, pathOut, Filter());

            (byte[] document, List<OfficeRedactionSpan> byteSpans) =
                WordDocumentRedactor.Redact(File.ReadAllBytes(input), Filter());
            string byteOut = Path_("byte.docx");
            File.WriteAllBytes(byteOut, document);

            Assert.Equal(pathSpans.Count, byteSpans.Count);
            Assert.Equal(WordDocs.AllBodyText(pathOut), WordDocs.AllBodyText(byteOut));
        }

        [Fact]
        public void Word_Detect_Bytes_MatchesPathOverload()
        {
            string input = Path_("in.docx");
            WordDocs.Create(input, "email carol@example.com", "ssn 123-45-6789");

            List<OfficeRedactionSpan> pathSpans = WordDocumentRedactor.Detect(input, Filter());
            List<OfficeRedactionSpan> byteSpans = WordDocumentRedactor.Detect(File.ReadAllBytes(input), Filter());

            Assert.Equal(pathSpans.Count, byteSpans.Count);
            Assert.Contains(byteSpans, s => s.Text == "carol@example.com");
        }

        [Fact]
        public void Word_ApplySpans_Bytes_RoundTrips()
        {
            string input = Path_("in.docx");
            WordDocs.Create(input, "call dave@example.com");
            byte[] inputBytes = File.ReadAllBytes(input);

            List<OfficeRedactionSpan> spans = WordDocumentRedactor.Detect(inputBytes, Filter());
            byte[] document = WordDocumentRedactor.ApplySpans(inputBytes, spans, highlight: false);

            string output = Path_("out.docx");
            File.WriteAllBytes(output, document);
            Assert.DoesNotContain("dave@example.com", WordDocs.AllBodyText(output));
        }

        [Fact]
        public void Word_ReadParagraphs_And_ReviewLines_Bytes_MatchPath()
        {
            string input = Path_("in.docx");
            WordDocs.Create(input, "alpha", "beta", "gamma");
            byte[] inputBytes = File.ReadAllBytes(input);

            Assert.Equal(WordDocumentRedactor.ReadParagraphs(input), WordDocumentRedactor.ReadParagraphs(inputBytes));
            Assert.Equal(WordDocumentRedactor.ReadReviewLines(input), WordDocumentRedactor.ReadReviewLines(inputBytes));
        }

        // --- Excel ----------------------------------------------------------------------------------

        private static readonly string?[][] Rows =
        {
            new string?[] { "Name", "Email", "Notes" },
            new string?[] { "Alice", "alice@example.com", "VIP" },
            new string?[] { "Bob", "bob@example.com", "ssn 123-45-6789" }
        };

        [Fact]
        public void Xlsx_Redact_Bytes_RedactsAndReturnsSpans()
        {
            string input = Path_("in.xlsx");
            SpreadsheetTestHelper.CreateXlsx(input, Rows);
            byte[] inputBytes = File.ReadAllBytes(input);

            (byte[] document, List<OfficeRedactionSpan> spans) = XlsxRedactor.Redact(inputBytes, Filter());

            Assert.NotEmpty(spans);
            string output = Path_("out.xlsx");
            File.WriteAllBytes(output, document);
            string text = SpreadsheetTestHelper.AllText(output);
            Assert.DoesNotContain("alice@example.com", text);
            Assert.DoesNotContain("bob@example.com", text);
            Assert.DoesNotContain("123-45-6789", text);
            Assert.Contains("VIP", text);
        }

        [Fact]
        public void Xlsx_Redact_Bytes_DoesNotMutateInputArray()
        {
            string input = Path_("in.xlsx");
            SpreadsheetTestHelper.CreateXlsx(input, Rows);
            byte[] inputBytes = File.ReadAllBytes(input);
            byte[] snapshot = (byte[])inputBytes.Clone();

            XlsxRedactor.Redact(inputBytes, Filter());

            Assert.Equal(snapshot, inputBytes);
        }

        [Fact]
        public void Xlsx_Redact_Bytes_MatchesPathOverload()
        {
            string input = Path_("in.xlsx");
            SpreadsheetTestHelper.CreateXlsx(input, Rows);

            string pathOut = Path_("path.xlsx");
            List<OfficeRedactionSpan> pathSpans = XlsxRedactor.Redact(input, pathOut, Filter());

            (byte[] document, List<OfficeRedactionSpan> byteSpans) = XlsxRedactor.Redact(File.ReadAllBytes(input), Filter());
            string byteOut = Path_("byte.xlsx");
            File.WriteAllBytes(byteOut, document);

            Assert.Equal(pathSpans.Count, byteSpans.Count);
            Assert.Equal(SpreadsheetTestHelper.AllText(pathOut), SpreadsheetTestHelper.AllText(byteOut));
        }

        [Fact]
        public void Xlsx_Detect_Bytes_MatchesPathOverload()
        {
            string input = Path_("in.xlsx");
            SpreadsheetTestHelper.CreateXlsx(input, Rows);

            List<OfficeRedactionSpan> pathSpans = XlsxRedactor.Detect(input, Filter());
            List<OfficeRedactionSpan> byteSpans = XlsxRedactor.Detect(File.ReadAllBytes(input), Filter());

            Assert.Equal(pathSpans.Count, byteSpans.Count);
        }

        [Fact]
        public void Xlsx_ApplySpans_Bytes_RoundTrips()
        {
            string input = Path_("in.xlsx");
            SpreadsheetTestHelper.CreateXlsx(input, Rows);
            byte[] inputBytes = File.ReadAllBytes(input);

            (byte[] _, List<OfficeRedactionSpan> spans) = XlsxRedactor.Redact(inputBytes, Filter());
            byte[] document = XlsxRedactor.ApplySpans(inputBytes, spans);

            string output = Path_("out.xlsx");
            File.WriteAllBytes(output, document);
            Assert.DoesNotContain("alice@example.com", SpreadsheetTestHelper.AllText(output));
        }

        [Fact]
        public void Xlsx_ReadSheetNames_And_ReadColumns_Bytes_MatchPath()
        {
            string input = Path_("in.xlsx");
            SpreadsheetTestHelper.CreateXlsx(input, Rows);
            byte[] inputBytes = File.ReadAllBytes(input);

            Assert.Equal(XlsxRedactor.ReadSheetNames(input), XlsxRedactor.ReadSheetNames(inputBytes));

            List<SpreadsheetColumn> pathCols = XlsxRedactor.ReadColumns(input);
            List<SpreadsheetColumn> byteCols = XlsxRedactor.ReadColumns(inputBytes);
            Assert.Equal(pathCols.Count, byteCols.Count);
            Assert.Equal(pathCols.Select(c => c.Header), byteCols.Select(c => c.Header));
        }
    }
}
