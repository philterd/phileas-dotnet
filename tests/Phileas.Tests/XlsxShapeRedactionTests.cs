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
    /// Free text in worksheet shapes and text boxes lives in the DrawingsPart's own drawing XML — separate
    /// from cells and from embedded charts. These verify it is scanned on redact, detected by verification,
    /// re-redacted on modify, and that PII split across runs is caught.
    /// </summary>
    public sealed class XlsxShapeRedactionTests : IDisposable
    {
        private readonly string _dir;
        private const string Email = "george@fake.com";

        public XlsxShapeRedactionTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "philter-shape-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best effort */ } }

        private static PhileasPolicy Policy() => new()
        {
            Name = "shape",
            Identifiers = new Identifiers { EmailAddress = new EmailAddress(), Ssn = new Ssn() }
        };

        private static Func<string, TextFilterResult> Filter =>
            t => new FilterService().Filter(Policy(), "ctx", 0, t);

        private static readonly IReadOnlyList<string?[]> Rows = new[] { new string?[] { "Sheet with a text box" } };

        [Fact]
        public void TextBox_WithPii_IsRedacted()
        {
            string input = Path.Combine(_dir, "in.xlsx");
            SpreadsheetTestHelper.CreateXlsxWithTextBox(input, Rows, $"Contact {Email} for details");
            string output = Path.Combine(_dir, "out.xlsx");

            XlsxRedactor.Redact(input, output, Filter);

            Assert.DoesNotContain(Email, SpreadsheetTestHelper.AllDrawingXml(output));
        }

        [Fact]
        public void TextBox_PiiSplitAcrossRuns_IsRedacted()
        {
            // The email spans two <a:t> runs; the paragraph merge must catch it.
            string input = Path.Combine(_dir, "split.xlsx");
            SpreadsheetTestHelper.CreateXlsxWithTextBox(input, Rows, "george@", "fake.com");
            string output = Path.Combine(_dir, "split-out.xlsx");

            XlsxRedactor.Redact(input, output, Filter);

            string drawing = SpreadsheetTestHelper.AllDrawingXml(output);
            Assert.DoesNotContain(Email, drawing);
            Assert.DoesNotContain("fake.com", drawing); // the second run must not survive either
        }

        [Fact]
        public void Detect_FindsShapePii()
        {
            string input = Path.Combine(_dir, "detect.xlsx");
            SpreadsheetTestHelper.CreateXlsxWithTextBox(input, Rows, $"Contact {Email}");

            List<OfficeRedactionSpan> residuals = XlsxRedactor.Detect(input, Filter);

            Assert.Contains(residuals, s => s.Text.Contains(Email));
        }

        [Fact]
        public void Modify_ApplySpans_RedactsShapeText()
        {
            string input = Path.Combine(_dir, "modify.xlsx");
            SpreadsheetTestHelper.CreateXlsxWithTextBox(input, Rows, $"Contact {Email}");
            List<OfficeRedactionSpan> spans = XlsxRedactor.Redact(input, Path.Combine(_dir, "scratch.xlsx"), Filter);
            string output = Path.Combine(_dir, "modify-out.xlsx");

            XlsxRedactor.ApplySpans(input, output, spans, policyFilter: Filter);

            Assert.DoesNotContain(Email, SpreadsheetTestHelper.AllDrawingXml(output));
        }

        [Fact]
        public void TextBox_NoPii_IsLeftIntact()
        {
            string input = Path.Combine(_dir, "clean.xlsx");
            SpreadsheetTestHelper.CreateXlsxWithTextBox(input, Rows, "Quarterly summary");
            string output = Path.Combine(_dir, "clean-out.xlsx");

            XlsxRedactor.Redact(input, output, Filter);

            Assert.Contains("Quarterly summary", SpreadsheetTestHelper.AllDrawingXml(output));
        }
    }
}
