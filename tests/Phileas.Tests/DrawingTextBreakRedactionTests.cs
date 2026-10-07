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

using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Phileas.Model;
using Phileas.Policy.Filters;
using Phileas.Services;
using Phileas.Services.Office;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;
using PhileasPolicy = Phileas.Policy.Policy;
using PolicyIdentifiers = Phileas.Policy.Identifiers;

namespace Phileas.Tests
{
    /// <summary>
    /// A DrawingML line break (&lt;a:br&gt;) in chart, SmartArt and text box text separates the words on
    /// either side in the text the filters see, and stays where it was when the text is rewritten.
    /// See philterd/phileas-dotnet#151.
    /// </summary>
    public sealed class DrawingTextBreakRedactionTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "phileas-drawing-breaks-" + Guid.NewGuid().ToString("N"));

        private static readonly PhileasPolicy NamePolicy = new()
        {
            Name = "names",
            Identifiers = new PolicyIdentifiers { FirstName = new FirstName(), Surname = new Surname() }
        };

        private static readonly PhileasPolicy EmailPolicy = new()
        {
            Name = "email",
            Identifiers = new PolicyIdentifiers { EmailAddress = new EmailAddress() }
        };

        private static readonly string[][] Grid = { new[] { "Name" }, new[] { "Value" } };

        public DrawingTextBreakRedactionTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        }

        private string NewPath(string extension) => Path.Combine(_dir, Guid.NewGuid().ToString("N") + extension);

        private static Func<string, TextFilterResult> FilterWith(PhileasPolicy policy, List<string>? seen = null)
        {
            var service = new FilterService();
            return text =>
            {
                seen?.Add(text);
                return service.Filter(policy, "ctx", 0, text);
            };
        }

        // A text body holding one paragraph. Each string is a run, and "\n" between strings is an <a:br>.
        private static A.Paragraph Paragraph(params string[] parts)
        {
            var paragraph = new A.Paragraph();
            foreach (string part in parts)
            {
                paragraph.AppendChild<OpenXmlElement>(part == "\n" ? new A.Break() : new A.Run(new A.Text(part)));
            }
            return paragraph;
        }

        private static string ParagraphXml(params string[] parts) =>
            Paragraph(parts).OuterXml.Replace(" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"", string.Empty);

        // A DrawingML paragraph as text, writing each <a:br> as "\n" and keeping run boundaries visible as "|".
        private static string Render(A.Paragraph paragraph)
        {
            var parts = new List<string>();
            foreach (OpenXmlElement child in paragraph.ChildElements)
            {
                if (child is A.Run run)
                {
                    parts.Add(string.Concat(run.Descendants<A.Text>().Select(t => t.Text)));
                }
                else if (child is A.Break)
                {
                    parts.Add("\n");
                }
            }
            return string.Join("|", parts);
        }

        private static (string Rendered, List<string> Seen) RedactDirectly(PhileasPolicy policy, A.Paragraph paragraph)
        {
            var root = new A.TextBody(new A.BodyProperties(), paragraph);
            var seen = new List<string>();
            int order = 0;
            ChartRedactor.RedactDrawingText(root, FilterWith(policy, seen), write: true, new List<OfficeRedactionSpan>(), ref order);
            return (Render(paragraph), seen);
        }

        // Every DrawingML paragraph in the package that holds text, rendered as in Render.
        private static List<string> DrawingParagraphs(string path)
        {
            using OpenXmlPackage package = path.EndsWith(".xlsx")
                ? SpreadsheetDocument.Open(path, false)
                : WordprocessingDocument.Open(path, false);
            var result = new List<string>();
            var seen = new HashSet<Uri>();
            var pending = new Stack<OpenXmlPart>(package.Parts.Select(p => p.OpenXmlPart));
            while (pending.Count > 0)
            {
                OpenXmlPart part = pending.Pop();
                if (!seen.Add(part.Uri))
                {
                    continue;
                }
                OpenXmlElement? root = null;
                try { root = part.RootElement; } catch { /* unparseable part */ }
                if (root is not null)
                {
                    result.AddRange(root.Descendants<A.Paragraph>().Where(p => p.Descendants<A.Text>().Any()).Select(Render));
                }
                foreach (IdPartPair child in part.Parts)
                {
                    pending.Push(child.OpenXmlPart);
                }
            }
            return result;
        }

        // --- The method directly: the cases from the issue ---------------------------------------------

        [Fact]
        public void TextPassedToTheFilter_HasALineFeedForEachBreak()
        {
            (_, List<string> seen) = RedactDirectly(NamePolicy, Paragraph("Patient George", "\n", "Washington was seen"));

            Assert.Equal("Patient George\nWashington was seen", Assert.Single(seen));
        }

        [Fact]
        public void NameSplitByBreak_IsRedacted_AndTheBreakStaysBetweenThem()
        {
            (string rendered, _) = RedactDirectly(NamePolicy, Paragraph("Patient George", "\n", "Washington was seen"));

            Assert.Equal("Patient {{{REDACTED-first-name}}}|\n|{{{REDACTED-first-name}}} was seen", rendered);
        }

        [Fact]
        public void NameAfterBreak_IsRedactedInFull()
        {
            (string rendered, _) = RedactDirectly(NamePolicy, Paragraph("Signed,", "\n", "George Washington"));

            Assert.Equal("Signed,|\n|{{{REDACTED-first-name}}} {{{REDACTED-first-name}}}", rendered);
        }

        [Theory]
        [InlineData("Write to john@example.com", "tomorrow please", "Write to {{{REDACTED-email-address}}}|\n|tomorrow please")]
        [InlineData("Write to contact", "john@example.com today", "Write to contact|\n|{{{REDACTED-email-address}}} today")]
        public void EmailNextToBreak_RemovesOnlyTheEmail(string before, string after, string expected)
        {
            (string rendered, _) = RedactDirectly(EmailPolicy, Paragraph(before, "\n", after));

            Assert.Equal(expected, rendered);
        }

        [Fact]
        public void SeveralBreaks_EachStayBetweenTheSameText()
        {
            (string rendered, _) = RedactDirectly(EmailPolicy,
                Paragraph("Line one", "\n", "a@example.com", "\n", "\n", "Line four"));

            Assert.Equal("Line one|\n|{{{REDACTED-email-address}}}|\n|\n|Line four", rendered);
        }

        [Fact]
        public void RunFormatting_IsKept()
        {
            var bold = new A.Run(new A.RunProperties { Bold = true }, new A.Text("Signed,"));
            var paragraph = new A.Paragraph(bold, new A.Break(), new A.Run(new A.Text("George Washington")));

            RedactDirectly(NamePolicy, paragraph);

            A.Run first = paragraph.Elements<A.Run>().First();
            Assert.True(first.RunProperties?.Bold?.Value);
            Assert.Equal("Signed,", first.Text!.Text);
        }

        [Fact]
        public void SpanCoveringABreak_IsWrittenWhereItStarts_AndTheBreakKept()
        {
            // A model that detects "George\nWashington" as one name returns one span across the break.
            var paragraph = Paragraph("Patient George", "\n", "Washington was seen");
            var root = new A.TextBody(new A.BodyProperties(), paragraph);
            Func<string, TextFilterResult> filter = _ =>
            {
                var span = Span.Make(8, 25, FilterType.PhEye, "ctx", 0.9, "George\nWashington", "{{{REDACTED-ph-eye}}}",
                    string.Empty, false, true, Array.Empty<string>(), 0);
                return new TextFilterResult("Patient {{{REDACTED-ph-eye}}} was seen", new List<Span> { span });
            };
            int order = 0;
            var captured = new List<OfficeRedactionSpan>();

            ChartRedactor.RedactDrawingText(root, filter, write: true, captured, ref order);

            Assert.Equal("Patient {{{REDACTED-ph-eye}}}|\n| was seen", Render(paragraph));
            Assert.Equal("George\nWashington", Assert.Single(captured).Text);
        }

        [Fact]
        public void FilterWhoseSpansDoNotMatchItsText_StillHasItsTextWritten()
        {
            // A hand-written delegate may return filtered text without spans that produce it. The text is
            // written whole, as before this change, rather than nothing being redacted.
            var paragraph = Paragraph("Patient George", "\n", "Washington was seen");
            var root = new A.TextBody(new A.BodyProperties(), paragraph);
            Func<string, TextFilterResult> filter = _ =>
                new TextFilterResult("Patient [name]\n[name] was seen", new List<Span>());
            int order = 0;

            ChartRedactor.RedactDrawingText(root, filter, write: true, new List<OfficeRedactionSpan>(), ref order);

            Assert.Equal("Patient [name] [name] was seen|\n|", Render(paragraph));
        }

        [Fact]
        public void SpanAcrossRuns_IsStillRedacted()
        {
            (string rendered, _) = RedactDirectly(EmailPolicy, Paragraph("Mail george@", "example.com now"));

            Assert.Equal("Mail {{{REDACTED-email-address}}}| now", rendered);
        }

        [Fact]
        public void DetectOnly_LeavesTheParagraphUnchanged()
        {
            var paragraph = Paragraph("Write to john@example.com", "\n", "tomorrow");
            var root = new A.TextBody(new A.BodyProperties(), paragraph);
            var captured = new List<OfficeRedactionSpan>();
            int order = 0;

            ChartRedactor.RedactDrawingText(root, FilterWith(EmailPolicy), write: false, captured, ref order);

            Assert.Equal("Write to john@example.com|\n|tomorrow", Render(paragraph));
            Assert.Equal("john@example.com", Assert.Single(captured).Text);
        }

        // --- Through whole documents --------------------------------------------------------------------

        [Fact]
        public void Docx_ChartTitle()
        {
            string input = NewPath(".docx");
            string output = NewPath(".docx");
            WordDocs.CreateWithChart(input, ParagraphXml("Signed,", "\n", "George Washington"), "Body.");

            WordDocumentRedactor.Redact(input, output, FilterWith(NamePolicy), redactCharts: true);

            Assert.Contains("Signed,|\n|{{{REDACTED-first-name}}} {{{REDACTED-first-name}}}", DrawingParagraphs(output));
        }

        [Fact]
        public void Docx_SmartArtNode()
        {
            string input = NewPath(".docx");
            string output = NewPath(".docx");
            WordDocs.CreateWithSmartArt(input, ParagraphXml("Write to john@example.com", "\n", "tomorrow please"), "Body.");

            WordDocumentRedactor.Redact(input, output, FilterWith(EmailPolicy));

            Assert.Contains("Write to {{{REDACTED-email-address}}}|\n|tomorrow please", DrawingParagraphs(output));
        }

        [Fact]
        public void Docx_ReviewLines_ShowTheBreak()
        {
            string input = NewPath(".docx");
            WordDocs.CreateWithSmartArt(input, ParagraphXml("Line one", "\n", "Line two"), "Body.");

            Assert.Contains("Line one\nLine two", WordDocumentRedactor.ReadReviewLines(input));
        }

        [Fact]
        public void Xlsx_ChartTitle()
        {
            string input = NewPath(".xlsx");
            string output = NewPath(".xlsx");
            SpreadsheetTestHelper.CreateXlsxWithChartXml(input, Grid,
                SpreadsheetTestHelper.ChartSpaceXmlWithTitleParagraph(ParagraphXml("Patient George", "\n", "Washington"), "Series", "Category"));

            XlsxRedactor.Redact(input, output, FilterWith(NamePolicy), redactCharts: true);

            Assert.Contains("Patient {{{REDACTED-first-name}}}|\n|{{{REDACTED-first-name}}}", DrawingParagraphs(output));
        }

        [Fact]
        public void Xlsx_TextBox()
        {
            string input = NewPath(".xlsx");
            string output = NewPath(".xlsx");
            SpreadsheetTestHelper.CreateXlsxWithTextBoxParagraph(input, Grid,
                ParagraphXml("Write to contact", "\n", "john@example.com today"));

            XlsxRedactor.Redact(input, output, FilterWith(EmailPolicy));

            Assert.Contains("Write to contact|\n|{{{REDACTED-email-address}}} today", DrawingParagraphs(output));
        }

        [DownloadModelFact]
        public void PhEye_NameSplitByBreak_IsRedacted_AndTheBreakStaysInPlace()
        {
            string dir = XsmallModel.EnsureDownloaded();
            var policy = new PhileasPolicy
            {
                Name = "pheye",
                Identifiers = new PolicyIdentifiers
                {
                    PhEyes = new List<PhEye>
                    {
                        new() { PhEyeConfiguration = new PhEyeConfiguration { ModelPath = dir, Labels = new List<string> { "name" }, Threshold = 0.5 } }
                    }
                }
            };

            (string rendered, _) = RedactDirectly(policy, Paragraph("Patient George", "\n", "Washington was seen"));

            Assert.DoesNotContain("George", rendered);
            Assert.DoesNotContain("Washington", rendered);
            // One span across the break or one per word: either way the break stays before " was seen".
            Assert.StartsWith("Patient ", rendered);
            Assert.EndsWith(" was seen", rendered);
            Assert.Equal(1, rendered.Split("|\n|").Length - 1);
        }
    }
}
