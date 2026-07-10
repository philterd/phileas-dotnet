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
using CellKind = Phileas.Tests.SpreadsheetTestHelper.CellKind;
using CellSpec = Phileas.Tests.SpreadsheetTestHelper.CellSpec;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests
{
    /// <summary>
    /// A formula cell stores a cached copy of its last computed value; when that duplicates PII from a
    /// now-redacted cell it would otherwise ship as plaintext. The "Redact cached formula values" option
    /// staticizes caches that hold detected PII and clears the rest (with recalc-on-load) when anything was
    /// redacted. When the option is off, formula cells are left untouched. Verification scans caches either way.
    /// </summary>
    public sealed class XlsxFormulaCacheTests : IDisposable
    {
        private readonly string _dir;

        public XlsxFormulaCacheTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "philter-formula-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best effort */ } }

        private static PhileasPolicy EmailPolicy() => new()
        {
            Name = "email",
            Identifiers = new Identifiers { EmailAddress = new EmailAddress() }
        };

        private static Func<string, TextFilterResult> Filter =>
            t => new FilterService().Filter(EmailPolicy(), "ctx", 0, t);

        // A2 holds the email; B2 is "=A2" whose cached result duplicates it; C2 is a benign formula.
        private string MakeWorkbook()
        {
            string path = Path.Combine(_dir, "book.xlsx");
            SpreadsheetTestHelper.CreateTyped(path, new List<CellSpec?[]>
            {
                new CellSpec?[] { "Name", "Copy", "Count" },
                new CellSpec?[]
                {
                    new CellSpec("george@fake.com"),
                    new CellSpec("george@fake.com", CellKind.Formula, "A2"),
                    new CellSpec("2", CellKind.Formula, "1+1")
                }
            });
            return path;
        }

        [Fact]
        public void Enabled_FormulaCacheWithPii_IsRedactedAndStaticized()
        {
            string input = MakeWorkbook();
            string output = Path.Combine(_dir, "out.xlsx");

            XlsxRedactor.Redact(input, output, Filter); // redactFormulaValues defaults to true

            // The email is gone everywhere — the cell and the formula's cached copy.
            Assert.DoesNotContain("george@fake.com", SpreadsheetTestHelper.AllText(output));
            // B2's cached PII was redacted and its formula dropped (so recalc can't restore it).
            Assert.False(SpreadsheetTestHelper.IsFormulaCell(output, "B2"));
        }

        [Fact]
        public void Enabled_OtherFormulaCaches_AreClearedAndRecalcForced()
        {
            string input = MakeWorkbook();
            string output = Path.Combine(_dir, "out.xlsx");

            XlsxRedactor.Redact(input, output, Filter);

            // C2 (a benign formula) keeps its formula but its stale cached value is cleared, and the
            // workbook recalculates on open so Excel recomputes it.
            Assert.True(SpreadsheetTestHelper.IsFormulaCell(output, "C2"));
            Assert.True(string.IsNullOrEmpty(SpreadsheetTestHelper.CachedValue(output, "C2")));
            Assert.True(SpreadsheetTestHelper.FullCalcOnLoad(output));
        }

        [Fact]
        public void Disabled_FormulaCells_AreLeftUntouched()
        {
            string input = MakeWorkbook();
            string output = Path.Combine(_dir, "out.xlsx");

            XlsxRedactor.Redact(input, output, Filter, redactFormulaValues: false);

            // The plain cell is still redacted...
            Assert.False(SpreadsheetTestHelper.IsFormulaCell(output, "A2"));
            // ...but the formula's cached copy of the email survives (the option is off).
            Assert.Equal("george@fake.com", SpreadsheetTestHelper.CachedValue(output, "B2"));
            Assert.True(SpreadsheetTestHelper.IsFormulaCell(output, "B2"));
            Assert.False(SpreadsheetTestHelper.FullCalcOnLoad(output));
        }

        [Fact]
        public void Detect_FindsPiiInFormulaCache_RegardlessOfOption()
        {
            // Detection (verification) always scans formula caches, so a residual left in one is caught.
            string input = MakeWorkbook();

            List<OfficeRedactionSpan> residuals = XlsxRedactor.Detect(input, Filter);

            Assert.Contains(residuals, s => s.Text.Contains("george@fake.com"));
        }

        [Fact]
        public void NoPii_FormulaCaches_AreLeftIntact()
        {
            // Nothing is redacted, so caches are preserved and recalc isn't forced (no needless changes).
            string input = Path.Combine(_dir, "clean.xlsx");
            SpreadsheetTestHelper.CreateTyped(input, new List<CellSpec?[]>
            {
                new CellSpec?[] { "A", "Sum" },
                new CellSpec?[] { new CellSpec("1"), new CellSpec("2", CellKind.Formula, "A2+1") }
            });
            string output = Path.Combine(_dir, "clean-out.xlsx");

            XlsxRedactor.Redact(input, output, Filter);

            Assert.True(SpreadsheetTestHelper.IsFormulaCell(output, "B2"));
            Assert.Equal("2", SpreadsheetTestHelper.CachedValue(output, "B2")); // cache intact
            Assert.False(SpreadsheetTestHelper.FullCalcOnLoad(output));
        }
    }
}
