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
    /// Non-breaking hyphens, soft hyphens and symbol characters in a Word paragraph are read the way the
    /// filters need them and survive redaction outside a redacted value. See philterd/phileas-dotnet#152.
    /// </summary>
    public class WordRunCharacterRedactionTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "phileas-word-runchars-" + Guid.NewGuid().ToString("N"));

        private static readonly PhileasPolicy EmailPolicy = new()
        {
            Name = "email",
            Identifiers = new PolicyIdentifiers { EmailAddress = new EmailAddress() }
        };

        private static readonly PhileasPolicy DatePolicy = new()
        {
            Name = "date",
            Identifiers = new PolicyIdentifiers { Date = new Date() }
        };

        private static readonly PhileasPolicy MrnPolicy = new()
        {
            Name = "mrn",
            Identifiers = new PolicyIdentifiers
            {
                CustomIdentifiers = new List<Identifier> { new() { Pattern = @"\bMRN-\d{4}-\d{4}\b", Classification = "mrn" } }
            }
        };

        private static readonly PhileasPolicy NamePolicy = new()
        {
            Name = "names",
            Identifiers = new PolicyIdentifiers { FirstName = new FirstName() }
        };

        public WordRunCharacterRedactionTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        }

        private static Func<string, TextFilterResult> FilterWith(PhileasPolicy policy)
        {
            var service = new FilterService();
            return text => service.Filter(policy, "ctx", 0, text);
        }

        private string CreateDocx(params OpenXmlElement[] runChildren)
        {
            string path = Path.Combine(_dir, "in_" + Guid.NewGuid().ToString("N") + ".docx");
            using WordprocessingDocument doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
            doc.AddMainDocumentPart().Document = new Document(new Body(new Paragraph(new Run(runChildren))));
            return path;
        }

        private string Output() => Path.Combine(_dir, "out_" + Guid.NewGuid().ToString("N") + ".docx");

        private static Text T(string text) => new(text) { Space = SpaceProcessingModeValues.Preserve };

        private static SymbolChar Phone() => new() { Font = "Wingdings", Char = "F028" };

        // The first body paragraph with each run-level character shown as a marker.
        private static string Render(string path)
        {
            using WordprocessingDocument doc = WordprocessingDocument.Open(path, false);
            Paragraph paragraph = doc.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().First();
            var builder = new StringBuilder();
            foreach (OpenXmlElement element in paragraph.Descendants())
            {
                builder.Append(element switch
                {
                    Text text => text.Text,
                    NoBreakHyphen => "[nbh]",
                    SoftHyphen => "[shy]",
                    SymbolChar symbol => $"[sym {symbol.Font} {symbol.Char}]",
                    Break => "[br]",
                    _ => string.Empty
                });
            }
            return builder.ToString();
        }

        [Fact]
        public void FilterText_HasAHyphenForANonBreakingHyphen_ASpaceForASymbol_AndNothingForASoftHyphen()
        {
            string input = CreateDocx(T("SSN 123"), new NoBreakHyphen(), T("45"), new NoBreakHyphen(), T("6789, call"),
                Phone(), T("Wash"), new SoftHyphen(), T("ington"));

            Assert.Equal(new[] { "SSN 123-45-6789, call Washington" }, WordDocumentRedactor.ReadParagraphs(input));
        }

        [Fact]
        public void DateWithNonBreakingHyphens_IsDetectedAndRedacted()
        {
            string input = CreateDocx(T("Seen 2026"), new NoBreakHyphen(), T("03"), new NoBreakHyphen(), T("15 in clinic"));
            string output = Output();

            OfficeRedactionSpan detected = Assert.Single(WordDocumentRedactor.Detect(input, FilterWith(DatePolicy)));
            WordDocumentRedactor.Redact(input, output, FilterWith(DatePolicy));

            Assert.Equal("2026-03-15", detected.Text);
            Assert.Equal("Seen {{{REDACTED-date}}} in clinic", Render(output));
        }

        [Fact]
        public void CustomIdentifierWithNonBreakingHyphens_IsDetectedAndRedacted()
        {
            string input = CreateDocx(T("Chart MRN"), new NoBreakHyphen(), T("4821"), new NoBreakHyphen(), T("0937 reviewed"));
            string output = Output();

            WordDocumentRedactor.Redact(input, output, FilterWith(MrnPolicy));

            Assert.Equal("Chart {{{REDACTED-identifier}}} reviewed", Render(output));
        }

        [Fact]
        public void NonBreakingHyphenOutsideTheValue_IsKept()
        {
            string input = CreateDocx(T("Ref A"), new NoBreakHyphen(), T("17 sent to a@example.com"));
            string output = Output();

            WordDocumentRedactor.Redact(input, output, FilterWith(EmailPolicy));

            Assert.Equal("Ref A[nbh]17 sent to {{{REDACTED-email-address}}}", Render(output));
        }

        [Fact]
        public void SoftHyphenOutsideTheValue_IsKept()
        {
            string input = CreateDocx(T("Contact Wash"), new SoftHyphen(), T("ington at a@example.com"));
            string output = Output();

            WordDocumentRedactor.Redact(input, output, FilterWith(EmailPolicy));

            Assert.Equal("Contact Wash[shy]ington at {{{REDACTED-email-address}}}", Render(output));
        }

        [Fact]
        public void SymbolOutsideTheValue_IsKeptWithItsFontAndCharacter()
        {
            string input = CreateDocx(T("Call"), Phone(), T("a@example.com"));
            string output = Output();

            WordDocumentRedactor.Redact(input, output, FilterWith(EmailPolicy));

            Assert.Equal("Call[sym Wingdings F028]{{{REDACTED-email-address}}}", Render(output));
        }

        [Fact]
        public void WordWithASoftHyphen_IsMatchedWhole_AndTheSoftHyphenGoesWithIt()
        {
            string input = CreateDocx(T("Dear Wash"), new SoftHyphen(), T("ington,"));
            string output = Output();

            WordDocumentRedactor.Redact(input, output, FilterWith(NamePolicy));

            Assert.Equal("Dear {{{REDACTED-first-name}}},", Render(output));
        }

        [Fact]
        public void SoftHyphens_AtTheStart_AndRightBeforeARedactedValue_AreKept()
        {
            // A soft hyphen joins the text around it, so the one before the value follows a space.
            string input = CreateDocx(new SoftHyphen(), T("Mail "), new SoftHyphen(), T("a@example.com"));
            string output = Output();

            WordDocumentRedactor.Redact(input, output, FilterWith(EmailPolicy));

            Assert.Equal("[shy]Mail [shy]{{{REDACTED-email-address}}}", Render(output));
        }

        [Fact]
        public void EachCharacterInsideARedactedValue_GoesWithIt_WhileABreakIsKept()
        {
            // "Ref A-17 B" then a break, then " end": the span covers "A-17 B" and the break after it.
            string input = CreateDocx(T("Ref A"), new NoBreakHyphen(), T("1"), new SoftHyphen(), T("7"), Phone(), T("B"),
                new Break(), T("end"));
            string output = Output();
            string paragraph = WordDocumentRedactor.ReadParagraphs(input)[0];
            Assert.Equal("Ref A-17 B\nend", paragraph);
            var span = new OfficeRedactionSpan
            {
                ParagraphIndex = 0, CharacterStart = 4, CharacterEnd = 11, Text = "A-17 B\n", Replacement = "X"
            };

            WordDocumentRedactor.ApplySpans(input, output, new List<OfficeRedactionSpan> { span }, highlight: false);

            Assert.Equal("Ref X[br]end", Render(output));
        }

        [Fact]
        public void SpansFromRedact_ReapplyThroughApplySpans()
        {
            string input = CreateDocx(T("Ref A"), new NoBreakHyphen(), T("17,"), Phone(), T("seen 2026"), new NoBreakHyphen(),
                T("03"), new NoBreakHyphen(), T("15, Wash"), new SoftHyphen(), T("ington"));
            string redacted = Output();
            string reapplied = Output();

            List<OfficeRedactionSpan> spans = WordDocumentRedactor.Redact(input, redacted, FilterWith(DatePolicy));
            WordDocumentRedactor.ApplySpans(input, reapplied, spans, highlight: false);

            string paragraph = WordDocumentRedactor.ReadParagraphs(input)[0];
            OfficeRedactionSpan date = Assert.Single(spans);
            Assert.Equal("2026-03-15", paragraph.Substring(date.CharacterStart, date.CharacterEnd - date.CharacterStart));
            Assert.Equal("Ref A[nbh]17,[sym Wingdings F028]seen {{{REDACTED-date}}}, Wash[shy]ington", Render(reapplied));
            Assert.Equal(Render(redacted), Render(reapplied));
        }
    }
}
