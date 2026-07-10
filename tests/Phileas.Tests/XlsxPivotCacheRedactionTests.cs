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
    /// A pivot table keeps a denormalized copy of its source data in the pivot cache (definition shared
    /// items + records). Redacting the sheet cells alone leaves that copy intact. These verify the cached
    /// values are redacted, the cache is set to refresh on open, indices survive, verification detects a
    /// residual, and the option can turn it off.
    /// </summary>
    public sealed class XlsxPivotCacheRedactionTests : IDisposable
    {
        private readonly string _dir;
        private const string Email = "george@fake.com";
        private const string Email2 = "mary@fake.com";

        public XlsxPivotCacheRedactionTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "philter-pivot-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best effort */ } }

        private static PhileasPolicy Policy() => new()
        {
            Name = "pivot",
            Identifiers = new Identifiers { EmailAddress = new EmailAddress(), Ssn = new Ssn() }
        };

        private static Func<string, TextFilterResult> Filter =>
            t => new FilterService().Filter(Policy(), "ctx", 0, t);

        private static readonly IReadOnlyList<string?[]> Rows = new[] { new string?[] { "Name" } };

        private string MakeWorkbook() // shared item email + inline-string record email
        {
            string path = Path.Combine(_dir, "in.xlsx");
            SpreadsheetTestHelper.CreateXlsxWithPivotCache(path, Rows, new[] { Email }, recordInlineString: Email2);
            return path;
        }

        [Fact]
        public void Enabled_RedactsSharedItemAndInlineRecord()
        {
            string input = MakeWorkbook();
            string output = Path.Combine(_dir, "out.xlsx");

            XlsxRedactor.Redact(input, output, Filter); // redactPivotCaches defaults to true

            string pivot = SpreadsheetTestHelper.AllPivotXml(output);
            Assert.DoesNotContain(Email, pivot);   // shared item (cache definition)
            Assert.DoesNotContain(Email2, pivot);  // inline value (cache records)
        }

        [Fact]
        public void Enabled_SetsRefreshOnLoad_AndKeepsIndices()
        {
            string input = MakeWorkbook();
            string output = Path.Combine(_dir, "refresh.xlsx");

            XlsxRedactor.Redact(input, output, Filter);

            Assert.True(SpreadsheetTestHelper.AnyPivotRefreshOnLoad(output));
            Assert.Contains("v=\"0\"", SpreadsheetTestHelper.AllPivotXml(output)); // the <x> index record is untouched
        }

        [Fact]
        public void Disabled_LeavesPivotCacheIntact()
        {
            string input = MakeWorkbook();
            string output = Path.Combine(_dir, "off.xlsx");

            XlsxRedactor.Redact(input, output, Filter, redactPivotCaches: false);

            string pivot = SpreadsheetTestHelper.AllPivotXml(output);
            Assert.Contains(Email, pivot);  // the copy survives when the option is off
            Assert.Contains(Email2, pivot);
        }

        [Fact]
        public void Detect_FindsPivotCachePii_RegardlessOfOption()
        {
            string input = MakeWorkbook();

            List<OfficeRedactionSpan> residuals = XlsxRedactor.Detect(input, Filter);

            Assert.Contains(residuals, s => s.Text.Contains(Email));
            Assert.Contains(residuals, s => s.Text.Contains(Email2));
        }

        [Fact]
        public void NoPii_PivotCacheLeftIntact()
        {
            string input = Path.Combine(_dir, "clean.xlsx");
            SpreadsheetTestHelper.CreateXlsxWithPivotCache(input, Rows, new[] { "North", "South" });
            string output = Path.Combine(_dir, "clean-out.xlsx");

            XlsxRedactor.Redact(input, output, Filter);

            string pivot = SpreadsheetTestHelper.AllPivotXml(output);
            Assert.Contains("North", pivot);
            Assert.Contains("South", pivot);
            Assert.False(SpreadsheetTestHelper.AnyPivotRefreshOnLoad(output)); // nothing redacted -> no forced refresh
        }
    }
}
