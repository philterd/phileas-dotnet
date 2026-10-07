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
using DocumentFormat.OpenXml.Wordprocessing;
using Phileas.Model;
using Phileas.Policy.Filters;
using Phileas.Services;
using Phileas.Services.Office;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;
using PolicyIdentifiers = Phileas.Policy.Identifiers;

namespace Phileas.Tests
{
    /// <summary>
    /// Soft breaks, carriage returns and tabs in a Word paragraph separate the words on either side in
    /// the text the filters see, and survive redaction. See philterd/phileas-dotnet#149.
    /// </summary>
    public class WordBreakRedactionTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "phileas-word-breaks-" + Guid.NewGuid().ToString("N"));

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

        public WordBreakRedactionTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        }

        private static Func<string, TextFilterResult> FilterWith(PhileasPolicy policy, List<string>? seen = null)
        {
            var service = new FilterService();
            return text =>
            {
                seen?.Add(text);
                return service.Filter(policy, "ctx", 0, text);
            };
        }

        // One body paragraph holding one run built from the given children.
        private string CreateDocx(params OpenXmlElement[] runChildren) => CreateDocx(new Paragraph(new Run(runChildren)));

        private string CreateDocx(Paragraph paragraph)
        {
            string path = Path.Combine(_dir, "in_" + Guid.NewGuid().ToString("N") + ".docx");
            using WordprocessingDocument doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
            doc.AddMainDocumentPart().Document = new Document(new Body(paragraph));
            return path;
        }

        private string Output() => Path.Combine(_dir, "out_" + Guid.NewGuid().ToString("N") + ".docx");

        private static Text T(string text) => new(text) { Space = SpaceProcessingModeValues.Preserve };

        // The first body paragraph as text, writing each break and carriage return as \n and each tab as \t.
        private static string Render(string path)
        {
            using WordprocessingDocument doc = WordprocessingDocument.Open(path, false);
            Paragraph paragraph = doc.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().First();
            var builder = new StringBuilder();
            foreach (OpenXmlElement element in paragraph.Descendants())
            {
                switch (element)
                {
                    case Text text: builder.Append(text.Text); break;
                    case Break or CarriageReturn: builder.Append('\n'); break;
                    case TabChar: builder.Append('\t'); break;
                }
            }
            return builder.ToString();
        }

        private static List<T> Elements<T>(string path) where T : OpenXmlElement
        {
            using WordprocessingDocument doc = WordprocessingDocument.Open(path, false);
            return doc.MainDocumentPart!.Document!.Body!.Descendants<T>().Select(e => (T)e.CloneNode(true)).ToList();
        }

        [Fact]
        public void BreaksAndTabs_GiveTheFiltersAndReadParagraphsOneCharacterEach()
        {
            string input = CreateDocx(T("A"), new Break(), T("B"), new CarriageReturn(), T("C"), new TabChar(), T("D"));
            var seen = new List<string>();

            WordDocumentRedactor.Redact(input, Output(), FilterWith(NamePolicy, seen));

            Assert.Equal("A\nB\nC\tD", Assert.Single(seen));
            Assert.Equal(new[] { "A\nB\nC\tD" }, WordDocumentRedactor.ReadParagraphs(input));
        }

        [Fact]
        public void NameSplitBySoftBreak_IsRedacted_AndTheBreakKept()
        {
            string input = CreateDocx(T("Patient George"), new Break(), T("Washington was seen today."));
            string output = Output();

            WordDocumentRedactor.Redact(input, output, FilterWith(NamePolicy));

            string text = Render(output);
            Assert.DoesNotContain("George", text);
            Assert.DoesNotContain("Washington", text);
            Assert.Equal("Patient {{{REDACTED-first-name}}}\n{{{REDACTED-first-name}}} was seen today.", text);
            Assert.Single(Elements<Break>(output));
        }

        [Fact]
        public void NameSplitByCarriageReturn_IsRedacted_AndTheCarriageReturnKept()
        {
            string input = CreateDocx(T("Patient George"), new CarriageReturn(), T("Washington was seen."));
            string output = Output();

            WordDocumentRedactor.Redact(input, output, FilterWith(NamePolicy));

            string text = Render(output);
            Assert.DoesNotContain("George", text);
            Assert.DoesNotContain("Washington", text);
            Assert.Single(Elements<CarriageReturn>(output));
        }

        [Fact]
        public void NameAfterSoftBreak_IsRedactedInFull()
        {
            string input = CreateDocx(T("Signed,"), new Break(), T("George Washington"));
            string output = Output();

            WordDocumentRedactor.Redact(input, output, FilterWith(NamePolicy));

            Assert.Equal("Signed,\n{{{REDACTED-first-name}}} {{{REDACTED-first-name}}}", Render(output));
        }

        [Fact]
        public void NameAfterTab_IsRedactedInFull_AndTheTabKept()
        {
            string input = CreateDocx(T("Name:"), new TabChar(), T("George Washington"));
            string output = Output();

            WordDocumentRedactor.Redact(input, output, FilterWith(NamePolicy));

            Assert.Equal("Name:\t{{{REDACTED-first-name}}} {{{REDACTED-first-name}}}", Render(output));
            Assert.Single(Elements<TabChar>(output));
        }

        [Fact]
        public void PositionalTab_IsATab_AndKeepsItsSettings()
        {
            var ptab = new PositionalTab
            {
                Alignment = AbsolutePositionTabAlignmentValues.Right,
                RelativeTo = AbsolutePositionTabPositioningBaseValues.Margin,
                Leader = AbsolutePositionTabLeaderCharValues.Dot
            };
            string input = CreateDocx(T("Name:"), ptab, T("George Washington"));
            string output = Output();

            Assert.Equal(new[] { "Name:\tGeorge Washington" }, WordDocumentRedactor.ReadParagraphs(input));

            WordDocumentRedactor.Redact(input, output, FilterWith(NamePolicy));

            PositionalTab kept = Assert.Single(Elements<PositionalTab>(output));
            Assert.Equal(AbsolutePositionTabAlignmentValues.Right, kept.Alignment!.Value);
            Assert.Equal(AbsolutePositionTabLeaderCharValues.Dot, kept.Leader!.Value);
            Assert.Equal(new[] { "Name:\t{{{REDACTED-first-name}}} {{{REDACTED-first-name}}}" }, WordDocumentRedactor.ReadParagraphs(output));
        }

        [Theory]
        [InlineData("Write to contact", "john@example.com today.", "Write to contact\n{{{REDACTED-email-address}}} today.")]
        [InlineData("Write to john@example.com", "tomorrow please.", "Write to {{{REDACTED-email-address}}}\ntomorrow please.")]
        public void EmailNextToSoftBreak_RemovesOnlyTheEmail(string before, string after, string expected)
        {
            string input = CreateDocx(T(before), new Break(), T(after));
            string output = Output();

            WordDocumentRedactor.Redact(input, output, FilterWith(EmailPolicy));

            Assert.Equal(expected, Render(output));
            Assert.Single(Elements<Break>(output));
        }

        [Fact]
        public void PageBreak_KeepsItsType()
        {
            string input = CreateDocx(T("Email john@example.com"), new Break { Type = BreakValues.Page }, T("Next page"));
            string output = Output();

            WordDocumentRedactor.Redact(input, output, FilterWith(EmailPolicy));

            Break kept = Assert.Single(Elements<Break>(output));
            Assert.Equal(BreakValues.Page, kept.Type!.Value);
            Assert.Equal("Email {{{REDACTED-email-address}}}\nNext page", Render(output));
        }

        [Fact]
        public void SpansFromRedact_IndexTheTextWithBreaks_AndReapplyThroughApplySpans()
        {
            string input = CreateDocx(T("Signed,"), new Break(), T("George"), new TabChar(), T("john@example.com"));
            var policy = new PhileasPolicy
            {
                Name = "both",
                Identifiers = new PolicyIdentifiers { FirstName = new FirstName(), EmailAddress = new EmailAddress() }
            };
            string redacted = Output();
            string reapplied = Output();

            List<OfficeRedactionSpan> spans = WordDocumentRedactor.Redact(input, redacted, FilterWith(policy));
            WordDocumentRedactor.ApplySpans(input, reapplied, spans, highlight: false);

            string paragraph = WordDocumentRedactor.ReadParagraphs(input)[0];
            Assert.Equal("Signed,\nGeorge\tjohn@example.com", paragraph);
            Assert.All(spans, s => Assert.Equal(s.Text, paragraph.Substring(s.CharacterStart, s.CharacterEnd - s.CharacterStart)));
            Assert.Contains(spans, s => s.Text == "George");
            Assert.Contains(spans, s => s.Text == "john@example.com");
            Assert.Equal(Render(redacted), Render(reapplied));
            Assert.Equal("Signed,\n{{{REDACTED-first-name}}}\t{{{REDACTED-email-address}}}", Render(reapplied));
        }

        [Fact]
        public void BreakInsideARedactedSpan_FollowsTheReplacement()
        {
            // A span covering the break itself, as a model detecting "George\nWashington" as one name
            // would return. The break is kept after the replacement rather than dropped.
            string input = CreateDocx(T("Patient George"), new Break(), T("Washington was seen."));
            string output = Output();
            var span = new OfficeRedactionSpan
            {
                ParagraphIndex = 0, CharacterStart = 8, CharacterEnd = 25, Text = "George\nWashington",
                Replacement = "{{{REDACTED-ph-eye}}}"
            };

            WordDocumentRedactor.ApplySpans(input, output, new List<OfficeRedactionSpan> { span }, highlight: false);

            Assert.Equal("Patient {{{REDACTED-ph-eye}}}\n was seen.", Render(output));
        }

        [Fact]
        public void BreakInATrackedDeletion_IsNotPartOfTheText()
        {
            var paragraph = new Paragraph(
                new Run(T("George")),
                new DeletedRun(new Run(new Break(), new DeletedText("x"))) { Author = "Reviewer", Id = "1" },
                new Run(T(" Washington")));
            string input = CreateDocx(paragraph);

            Assert.Equal(new[] { "George Washington" }, WordDocumentRedactor.ReadParagraphs(input));
        }

        [Fact]
        public void BreakOnlyRunInADrawingParagraph_IsNotDuplicated()
        {
            // A picture makes the paragraph "complex", so only its own text runs are rebuilt. A run holding
            // just a break is one of them: left in place, it would be emitted twice.
            var paragraph = new Paragraph(
                new Run(new Picture()),
                new Run(T("Email john@example.com")),
                new Run(new Break()),
                new Run(T("Next line")));
            string input = CreateDocx(paragraph);
            string output = Output();

            WordDocumentRedactor.Redact(input, output, FilterWith(EmailPolicy));

            Assert.Equal("Email {{{REDACTED-email-address}}}\nNext line", Render(output));
            Assert.Single(Elements<Break>(output));
            Assert.Single(Elements<Picture>(output));
        }

        [Fact]
        public void ParagraphWithBreaksAndNoPii_IsLeftUntouched()
        {
            string input = CreateDocx(T("Line one"), new Break(), T("Line two"), new TabChar(), T("end"));
            string output = Output();

            WordDocumentRedactor.Redact(input, output, FilterWith(EmailPolicy));

            Assert.Equal(Render(input), Render(output));
            Assert.Single(Elements<Break>(output));
            Assert.Single(Elements<TabChar>(output));
        }

        [DownloadModelFact]
        public void PhEye_RedactsANameSplitBySoftBreak()
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
            string input = CreateDocx(T("Patient George"), new Break(), T("Washington was seen today."));
            string output = Output();

            WordDocumentRedactor.Redact(input, output, FilterWith(policy));

            string text = Render(output);
            Assert.DoesNotContain("George", text);
            Assert.DoesNotContain("Washington", text);
            Assert.EndsWith(" was seen today.", text);
            Assert.Single(Elements<Break>(output));
        }
    }
}
