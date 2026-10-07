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

using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;
using Phileas.Services;
using Phileas.Services.Office;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
///     Detect reports a paragraph's or cell's spans only when the filter changed its text, as Redact does, so the
///     two return the same spans when a span's replacement is the original text. See philterd/phileas-dotnet#155.
/// </summary>
public sealed class OfficeDetectUnchangedTextTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "phileas-detect-unchanged-" + Guid.NewGuid().ToString("N"));

    public OfficeDetectUnchangedTextTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string NewPath(string extension) => Path.Combine(_dir, Guid.NewGuid().ToString("N") + extension);

    private static string Describe(OfficeRedactionSpan s) =>
        $"{s.Order}|{s.ParagraphIndex}|{s.CharacterStart}-{s.CharacterEnd}|{s.Text}|{s.Replacement}";

    // Reports the address without replacing it: the filtered text is the input.
    private static TextFilterResult ReportWithoutReplacing(string text)
    {
        var i = text.IndexOf("a@example.com", StringComparison.Ordinal);
        if (i < 0) return new TextFilterResult(text, new List<Span>());
        var span = Span.Make(i, i + 13, FilterType.EmailAddress, "ctx", 1.0, "a@example.com", "a@example.com",
            string.Empty, false, false, Array.Empty<string>(), 0);
        return new TextFilterResult(text, new List<Span> { span });
    }

    // SAME is not in the schema's strategy list, so it can only be set in code.
    private static Func<string, TextFilterResult> SameSsnFilter(bool redactEmails = false)
    {
        var identifiers = new Identifiers
        {
            Ssn = new Ssn { Strategies = new List<SsnFilterStrategy> { new() { Strategy = "SAME" } } }
        };
        if (redactEmails) identifiers.EmailAddress = new EmailAddress();
        var policy = new PhileasPolicy { Name = "p", Identifiers = identifiers };
        var service = new FilterService();
        return text => service.Filter(policy, "ctx", 0, text);
    }

    [Fact]
    public void Word_ASpanThatLeavesTheTextUnchanged_IsReportedByNeither()
    {
        string input = NewPath(".docx");
        WordDocs.Create(input, "Mail a@example.com now");

        var detected = WordDocumentRedactor.Detect(input, ReportWithoutReplacing);
        var redacted = WordDocumentRedactor.Redact(input, NewPath(".docx"), ReportWithoutReplacing);

        Assert.Empty(detected);
        Assert.Empty(redacted);
    }

    [Fact]
    public void Word_ASameStrategy_IsReportedByNeither()
    {
        string input = NewPath(".docx");
        WordDocs.Create(input, "ssn 123-45-6789 on file");

        Assert.Empty(WordDocumentRedactor.Detect(input, SameSsnFilter()));
        Assert.Empty(WordDocumentRedactor.Redact(input, NewPath(".docx"), SameSsnFilter()));
    }

    [Fact]
    public void Word_AParagraphTheFilterChanges_IsReportedWithEverySpanByBoth()
    {
        // The SAME span sits in a paragraph that another span changes, so both methods report both.
        string input = NewPath(".docx");
        WordDocs.Create(input, "ssn 123-45-6789 and mail b@example.com");

        var detected = WordDocumentRedactor.Detect(input, SameSsnFilter(redactEmails: true));
        var redacted = WordDocumentRedactor.Redact(input, NewPath(".docx"), SameSsnFilter(redactEmails: true));

        Assert.Equal(2, detected.Count);
        Assert.Equal(redacted.Select(Describe), detected.Select(Describe));
    }

    [Fact]
    public void Excel_ASpanThatLeavesTheTextUnchanged_IsReportedByNeither()
    {
        string input = NewPath(".xlsx");
        SpreadsheetTestHelper.CreateXlsx(input, new[] { new string?[] { "Mail a@example.com now" } });

        Assert.Empty(XlsxRedactor.Detect(input, ReportWithoutReplacing));
        Assert.Empty(XlsxRedactor.Redact(input, NewPath(".xlsx"), ReportWithoutReplacing));
    }

    [Fact]
    public void Excel_ACellTheFilterChanges_IsReportedWithEverySpanByBoth()
    {
        string input = NewPath(".xlsx");
        SpreadsheetTestHelper.CreateXlsx(input, new[] { new string?[] { "ssn 123-45-6789 and mail b@example.com" } });

        var detected = XlsxRedactor.Detect(input, SameSsnFilter(redactEmails: true));
        var redacted = XlsxRedactor.Redact(input, NewPath(".xlsx"), SameSsnFilter(redactEmails: true));

        Assert.Equal(2, detected.Count);
        Assert.Equal(redacted.Select(Describe), detected.Select(Describe));
    }

    [Fact]
    public void Excel_ASameStrategy_IsReportedByNeither()
    {
        string input = NewPath(".xlsx");
        SpreadsheetTestHelper.CreateXlsx(input, new[] { new string?[] { "ssn 123-45-6789 on file" } });

        Assert.Empty(XlsxRedactor.Detect(input, SameSsnFilter()));
        Assert.Empty(XlsxRedactor.Redact(input, NewPath(".xlsx"), SameSsnFilter()));
    }
}
