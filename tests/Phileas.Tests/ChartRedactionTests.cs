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

using Phileas.Policy;
using Phileas.Policy.Filters;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests
{
    /// <summary>
    /// Embedded charts (in both .docx and .xlsx) carry PII in their title/label text and — the part that
    /// otherwise survives — the cached series and category values that copy the source cells. Both must be
    /// redacted when the "Redact charts" option is on, and left alone when it's off.
    /// </summary>
    public sealed class ChartRedactionTests : IDisposable
    {
        private readonly string _dir;

        public ChartRedactionTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "philter-chart-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best effort */ } }

        private static readonly PhileasPolicy Policy = new()
        {
            Name = "chart",
            Identifiers = new Identifiers { EmailAddress = new EmailAddress(), Ssn = new Ssn() }
        };

        private static Func<string, TextFilterResult> Filter =>
            t => new FilterService().Filter(Policy, "ctx", 0, t);

        private const string Title = "Report for john@example.com";
        private const string SeriesName = "SSN 123-45-6789";  // cached series name (strCache)
        private const string Category = "alice@example.com";  // cached category (strCache)

        private static readonly string[][] Grid = { new[] { "Name" }, new[] { "Alice" } };

        // --- .docx ------------------------------------------------------------

        [Fact]
        public void Docx_ChartOn_RedactsTitleAndCachedValues()
        {
            string input = Path.Combine(_dir, "chart.docx");
            WordDocs.CreateWithChartData(input, Title, SeriesName, Category, "Body.");
            string output = Path.Combine(_dir, "chart-out.docx");

            WordDocumentRedactor.Redact(input, output, Filter, highlight: false, redactHeadersFooters: true, redactCharts: true);

            Assert.False(WordDocs.AnyPartContains(output, "john@example.com")); // chart title (<a:t>)
            Assert.False(WordDocs.AnyPartContains(output, "123-45-6789"));      // cached series name (<c:v>)
            Assert.False(WordDocs.AnyPartContains(output, "alice@example.com")); // cached category (<c:v>)
        }

        [Fact]
        public void Docx_ChartOff_LeavesChartUntouched()
        {
            string input = Path.Combine(_dir, "chart-off.docx");
            WordDocs.CreateWithChartData(input, Title, SeriesName, Category, "Body.");
            string output = Path.Combine(_dir, "chart-off-out.docx");

            WordDocumentRedactor.Redact(input, output, Filter, highlight: false, redactHeadersFooters: true, redactCharts: false);

            Assert.True(WordDocs.AnyPartContains(output, "john@example.com"));
            Assert.True(WordDocs.AnyPartContains(output, "123-45-6789"));
        }

        [Fact]
        public void Docx_Detect_FindsChartPii()
        {
            string input = Path.Combine(_dir, "chart-detect.docx");
            WordDocs.CreateWithChartData(input, Title, SeriesName, Category, "Body.");

            List<OfficeRedactionSpan> residuals = WordDocumentRedactor.Detect(input, Filter);

            Assert.Contains(residuals, s => s.Text.Contains("john@example.com"));
            Assert.Contains(residuals, s => s.Text.Contains("123-45-6789"));
        }

        // --- .xlsx ------------------------------------------------------------

        [Fact]
        public void Xlsx_ChartOn_RedactsTitleAndCachedValues()
        {
            string input = Path.Combine(_dir, "chart.xlsx");
            SpreadsheetTestHelper.CreateXlsxWithChart(input, Grid, Title, SeriesName, Category);
            string output = Path.Combine(_dir, "chart-out.xlsx");

            XlsxRedactor.Redact(input, output, Filter);

            string chartXml = SpreadsheetTestHelper.AllChartXml(output);
            Assert.DoesNotContain("john@example.com", chartXml); // title
            Assert.DoesNotContain("123-45-6789", chartXml);      // cached series name
            Assert.DoesNotContain("alice@example.com", chartXml); // cached category
        }

        [Fact]
        public void Xlsx_ChartOff_LeavesChartUntouched()
        {
            string input = Path.Combine(_dir, "chart-off.xlsx");
            SpreadsheetTestHelper.CreateXlsxWithChart(input, Grid, Title, SeriesName, Category);
            string output = Path.Combine(_dir, "chart-off-out.xlsx");

            XlsxRedactor.Redact(input, output, Filter, redactCharts: false);

            Assert.Contains("john@example.com", SpreadsheetTestHelper.AllChartXml(output));
        }

        [Fact]
        public void Xlsx_Detect_FindsChartPii()
        {
            string input = Path.Combine(_dir, "chart-detect.xlsx");
            SpreadsheetTestHelper.CreateXlsxWithChart(input, Grid, Title, SeriesName, Category);

            List<OfficeRedactionSpan> residuals = XlsxRedactor.Detect(input, Filter);

            Assert.Contains(residuals, s => s.Text.Contains("john@example.com"));
        }
    }
}
