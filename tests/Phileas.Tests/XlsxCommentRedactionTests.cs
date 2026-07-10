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
    /// Excel cell comments — both the legacy store (xl/comments*.xml) and the modern threaded comments
    /// (xl/threadedComments/*.xml, with authors in xl/persons/*.xml) — carry free text that isn't in any
    /// cell. Their PII must be redacted, not shipped verbatim.
    /// </summary>
    public sealed class XlsxCommentRedactionTests : IDisposable
    {
        private readonly string _dir;

        public XlsxCommentRedactionTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "philter-xlsxc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best effort */ } }

        private static readonly PhileasPolicy Policy = new()
        {
            Name = "c",
            Identifiers = new Identifiers { EmailAddress = new EmailAddress(), Ssn = new Ssn() }
        };

        private static Func<string, TextFilterResult> Filter =>
            t => new FilterService().Filter(Policy, "ctx", 0, t);

        private static readonly string[][] Grid = { new[] { "Name" }, new[] { "Alice" } };

        [Fact]
        public void Redact_LegacyComment_RedactsCommentText_KeepsTheComment()
        {
            string input = Path.Combine(_dir, "legacy.xlsx");
            SpreadsheetTestHelper.CreateXlsxWithLegacyComment(input, Grid, "A1", "Follow up with john@example.com re SSN 123-45-6789.");
            string output = Path.Combine(_dir, "legacy-out.xlsx");

            XlsxRedactor.Redact(input, output, Filter);

            string comments = SpreadsheetTestHelper.AllCommentText(output);
            Assert.NotEmpty(comments);                       // the comment is preserved
            Assert.DoesNotContain("john@example.com", comments);
            Assert.DoesNotContain("123-45-6789", comments);
            Assert.Contains("REDACTED", comments);
            Assert.DoesNotContain("john@example.com", SpreadsheetTestHelper.AllCommentXml(output)); // strong leak check
        }

        [Fact]
        public void Redact_ThreadedComment_RedactsTextAndAuthorPii()
        {
            string input = Path.Combine(_dir, "threaded.xlsx");
            SpreadsheetTestHelper.CreateXlsxWithThreadedComment(input, Grid, "A1",
                commentText: "Please verify john@example.com.", authorDisplayName: "mary@example.com");
            string output = Path.Combine(_dir, "threaded-out.xlsx");

            XlsxRedactor.Redact(input, output, Filter);

            string xml = SpreadsheetTestHelper.AllCommentXml(output);
            Assert.DoesNotContain("john@example.com", xml); // threaded comment text
            Assert.DoesNotContain("mary@example.com", xml); // person display name
        }

        [Fact]
        public void Detect_FindsPiiInComment()
        {
            string input = Path.Combine(_dir, "detect.xlsx");
            SpreadsheetTestHelper.CreateXlsxWithLegacyComment(input, Grid, "A1", "Reach john@example.com.");

            List<OfficeRedactionSpan> residuals = XlsxRedactor.Detect(input, Filter);

            Assert.Contains(residuals, s => s.Text.Contains("john@example.com"));
        }

        [Fact]
        public void ApplySpans_WithFilter_RedactsComment_LikeModify()
        {
            string input = Path.Combine(_dir, "apply.xlsx");
            SpreadsheetTestHelper.CreateXlsxWithLegacyComment(input, Grid, "A1", "Old note: john@example.com.");
            string output = Path.Combine(_dir, "apply-out.xlsx");

            // Modify re-renders from the source, passing the policy filter for the non-cell passes.
            XlsxRedactor.ApplySpans(input, output, new List<OfficeRedactionSpan>(), worksheet: null, policyFilter: Filter);

            Assert.DoesNotContain("john@example.com", SpreadsheetTestHelper.AllCommentXml(output));
        }

        [Fact]
        public void Redact_NonPiiComment_IsUnchanged()
        {
            string input = Path.Combine(_dir, "plain.xlsx");
            SpreadsheetTestHelper.CreateXlsxWithLegacyComment(input, Grid, "A1", "Looks good to me.");
            string output = Path.Combine(_dir, "plain-out.xlsx");

            XlsxRedactor.Redact(input, output, Filter);

            Assert.Contains("Looks good to me.", SpreadsheetTestHelper.AllCommentText(output));
        }
    }
}
