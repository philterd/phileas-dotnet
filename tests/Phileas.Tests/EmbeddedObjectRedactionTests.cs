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
    /// An embedded object (Insert &gt; Object) carries its own full content. An embedded Word/Excel document
    /// is redacted in place by recursing the matching redactor into it; an opaque OLE object we can't read is
    /// removed (default) or kept with a verification caveat. Covers both the .docx and .xlsx containers.
    /// </summary>
    public sealed class EmbeddedObjectRedactionTests : IDisposable
    {
        private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        private const string Email = "george@fake.com";

        private static readonly PhileasPolicy EmailPolicy = new()
        {
            Name = "email",
            Identifiers = new Phileas.Policy.Identifiers { EmailAddress = new Phileas.Policy.Filters.EmailAddress() }
        };

        private static Func<string, TextFilterResult> Filter =>
            t => new FilterService().Filter(EmailPolicy, "ctx", 0, t);

        private readonly string _dir;

        public EmbeddedObjectRedactionTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "philter-embobj-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best effort */ } }

        private string P(string name) => Path.Combine(_dir, name);

        private byte[] XlsxWithEmail()
        {
            string tmp = P("emb.xlsx");
            SpreadsheetTestHelper.CreateXlsx(tmp, new List<string?[]>
            {
                new string?[] { "Name", "Email" },
                new string?[] { "Bob", Email }
            });
            return File.ReadAllBytes(tmp);
        }

        private byte[] DocxWithEmail()
        {
            string tmp = P("emb.docx");
            WordDocs.Create(tmp, $"Reach {Email} for details.");
            return File.ReadAllBytes(tmp);
        }

        // --- docx container -----------------------------------------------------------

        [Fact]
        public void Docx_EmbeddedXlsx_IsRedactedInPlace()
        {
            string input = P("host.docx");
            string output = P("host_out.docx");
            WordDocs.CreateWithEmbeddedPackage(input, XlsxWithEmail(), Xlsx, "See attached.");

            WordDocumentRedactor.Redact(input, output, Filter);

            Assert.True(WordDocs.HasAnyEmbeddedObject(output), "a readable Office document is redacted in place, not removed");
            Assert.DoesNotContain(Email, WordDocs.EmbeddedPackageAllTextAsXlsx(output));
        }

        [Fact]
        public void Docx_EmbeddedDocx_IsRedactedInPlace()
        {
            string input = P("hostd.docx");
            string output = P("hostd_out.docx");
            WordDocs.CreateWithEmbeddedPackage(input, DocxWithEmail(), Docx, "See attached.");

            WordDocumentRedactor.Redact(input, output, Filter);

            Assert.DoesNotContain(Email, WordDocs.EmbeddedPackageAllTextAsDocx(output));
        }

        [Fact]
        public void Docx_OpaqueOleObject_RemovedByDefault()
        {
            string input = P("ole.docx");
            string output = P("ole_out.docx");
            byte[] garbage = System.Text.Encoding.UTF8.GetBytes("legacy OLE blob with " + Email);
            WordDocs.CreateWithEmbeddedOleObject(input, garbage, "Object below.");

            WordDocumentRedactor.Redact(input, output, Filter); // removeEmbeddedObjects defaults to true

            Assert.False(WordDocs.HasAnyEmbeddedObject(output), "an un-inspectable object must be removed");
            Assert.False(WordDocs.AnyPartContains(output, Email));
        }

        [Fact]
        public void Docx_OpaqueOleObject_KeptWhenDisabled_AndCaveatSurfaces()
        {
            string input = P("keep.docx");
            string output = P("keep_out.docx");
            byte[] garbage = System.Text.Encoding.UTF8.GetBytes("legacy OLE blob");
            WordDocs.CreateWithEmbeddedOleObject(input, garbage, "Object below.");

            WordDocumentRedactor.Redact(input, output, Filter, removeEmbeddedObjects: false);

            Assert.True(WordDocs.HasAnyEmbeddedObject(output), "the object is kept when the option is off");
            Assert.True(EmbeddedObjectRedactor.DocxHasUninspectable(output), "verification should flag the kept object");
        }

        [Fact]
        public void Detect_FindsPiiInEmbeddedXlsx()
        {
            string input = P("detect.docx");
            WordDocs.CreateWithEmbeddedPackage(input, XlsxWithEmail(), Xlsx, "See attached.");

            List<OfficeRedactionSpan> spans = WordDocumentRedactor.Detect(input, Filter);

            Assert.Contains(spans, s => s.Text.Contains(Email));
        }

        // --- xlsx container -----------------------------------------------------------

        [Fact]
        public void Xlsx_EmbeddedXlsx_IsRedactedInPlace()
        {
            string input = P("host.xlsx");
            string output = P("host_out.xlsx");
            SpreadsheetTestHelper.CreateXlsxWithEmbeddedPackage(
                input, new List<string?[]> { new string?[] { "A" } }, XlsxWithEmail(), Xlsx);

            XlsxRedactor.Redact(input, output, Filter);

            Assert.True(SpreadsheetTestHelper.XlsxHasAnyEmbeddedObject(output));
            Assert.DoesNotContain(Email, SpreadsheetTestHelper.XlsxEmbeddedPackageAllText(output));
        }

        [Fact]
        public void Xlsx_OpaqueOleObject_RemovedByDefault()
        {
            string input = P("ole.xlsx");
            string output = P("ole_out.xlsx");
            byte[] garbage = System.Text.Encoding.UTF8.GetBytes("legacy OLE blob with " + Email);
            SpreadsheetTestHelper.CreateXlsxWithEmbeddedOleObject(input, new List<string?[]> { new string?[] { "A" } }, garbage);

            XlsxRedactor.Redact(input, output, Filter);

            Assert.False(SpreadsheetTestHelper.XlsxHasAnyEmbeddedObject(output));
        }
    }
}
