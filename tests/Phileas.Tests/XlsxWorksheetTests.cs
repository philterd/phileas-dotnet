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
    /// .xlsx redaction targets a single chosen worksheet: the column picker reads that sheet's headers,
    /// and redaction (per-cell and whole-column) applies only to that sheet, leaving the others untouched.
    /// </summary>
    public sealed class XlsxWorksheetTests : IDisposable
    {
        private readonly string _dir;

        public XlsxWorksheetTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "philter-xlsx-ws-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        }

        private static readonly PhileasPolicy EmailPolicy = new()
        {
            Name = "email",
            Identifiers = new Identifiers { EmailAddress = new EmailAddress() }
        };

        private static Func<string, TextFilterResult> Filter =>
            t => new FilterService().Filter(EmailPolicy, "ctx", 0, t);

        // A workbook with a "People" sheet and a "Vendors" sheet, each with a header row and PII.
        private string TwoSheetWorkbook()
        {
            string path = Path.Combine(_dir, "book.xlsx");
            SpreadsheetTestHelper.CreateXlsx(path, new string?[][]
            {
                new string?[] { "Name", "Email" },
                new string?[] { "John", "john@example.com" }
            }, "People");
            SpreadsheetTestHelper.AppendSheet(path, new string?[][]
            {
                new string?[] { "Company", "Contact" },
                new string?[] { "Acme", "jane@example.com" }
            }, "Vendors");
            return path;
        }

        [Fact]
        public void ReadSheetNames_ReturnsSheetsInOrder()
        {
            Assert.Equal(new[] { "People", "Vendors" }, XlsxRedactor.ReadSheetNames(TwoSheetWorkbook()));
        }

        [Fact]
        public void ReadColumns_ReadsTheChosenSheetsHeaders()
        {
            string path = TwoSheetWorkbook();

            List<SpreadsheetColumn> vendors = XlsxRedactor.ReadColumns(path, "Vendors");
            Assert.Contains(vendors, c => c.Header == "Company");
            Assert.Contains(vendors, c => c.Header == "Contact");
            Assert.DoesNotContain(vendors, c => c.Header == "Name"); // that's the People sheet

            // A null/omitted sheet name defaults to the first sheet (back-compat).
            Assert.Contains(XlsxRedactor.ReadColumns(path), c => c.Header == "Name");
        }

        [Fact]
        public void Redact_OnlyTheChosenWorksheet_LeavesOtherSheetsUntouched()
        {
            string path = TwoSheetWorkbook();
            string output = Path.Combine(_dir, "out.xlsx");

            XlsxRedactor.Redact(path, output, Filter, fullyRedactedColumns: null, worksheet: "Vendors");

            string all = SpreadsheetTestHelper.AllText(output);
            Assert.DoesNotContain("jane@example.com", all); // Vendors was redacted
            Assert.Contains("john@example.com", all);        // People was not touched
        }

        [Fact]
        public void Redact_FullColumn_AppliesOnlyToTheChosenWorksheet()
        {
            string path = TwoSheetWorkbook();
            string output = Path.Combine(_dir, "out.xlsx");

            // Fully redact column 1 (the first column) — but only on the People sheet.
            XlsxRedactor.Redact(path, output, Filter, fullyRedactedColumns: new[] { 1 }, worksheet: "People");

            string all = SpreadsheetTestHelper.AllText(output);
            Assert.DoesNotContain("John", all); // People column-1 data cleared
            Assert.Contains("Name", all);        // its header kept
            Assert.Contains("Acme", all);        // Vendors column-1 data untouched
            Assert.Contains("Company", all);     // Vendors header untouched
        }
    }
}
