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

using Phileas.Filters;
using Phileas.Filters.Rules.Regex.RegexFilters;
using Phileas.Filters.Strategies.Rules;
using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

public class SsnFilterTests
{
    private static SsnFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new SsnFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new SsnFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Ssn = new Ssn() }
        };
    }

    [Theory]
    [InlineData("SSN: 123-45-6789")]
    [InlineData("My SSN is 123 45 6789")]
    [InlineData("Social security: 123456789")]
    public void Filter_DetectsSsn(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
    }

    [Fact]
    public void Filter_ReturnsCorrectFilterType()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, "SSN: 123-45-6789");
        Assert.Equal(FilterType.Ssn, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("SSN: 000-45-6789")] // Area 000 is invalid
    [InlineData("SSN: 666-45-6789")] // Area 666 is invalid
    [InlineData("SSN: 900-45-6789")] // Area 9xx is invalid
    public void Filter_DoesNotDetectInvalidSsnPrefix(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.Empty(result.Spans);
    }

    [Theory]
    [InlineData("SSN: 123-00-6789")] // Group 00 is invalid
    [InlineData("SSN: 123-45-0000")] // Serial 0000 is invalid
    public void Filter_DoesNotDetectInvalidSsnGroupOrSerial(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void Filter_EmptyInput_ReturnsNoSpans()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, string.Empty);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void Filter_NoSsnInText_ReturnsNoSpans()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, "No sensitive data here.");
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void Filter_ReturnsCorrectSpanPositions()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        const string input = "SSN: 123-45-6789 end";
        var result = filter.Filter(policy, "test", 0, input);
        Assert.Single(result.Spans);
        Assert.Equal("123-45-6789", result.Spans[0].Text);
        Assert.Equal(5, result.Spans[0].CharacterStart);
        Assert.Equal(16, result.Spans[0].CharacterEnd);
    }

    // -------------------------------------------------------------------------
    // Separators: hyphen substitutes, horizontal whitespace, and wrapped identifiers.
    // Every value below is synthetic. See philterd/phileas-dotnet#93.
    // -------------------------------------------------------------------------

    /// <summary>Asserts the identifier is a single span at the expected offsets, and is redacted.</summary>
    private static void AssertRedacted(string input, string identifier)
    {
        var start = input.IndexOf(identifier, StringComparison.Ordinal);
        Assert.True(start >= 0, "the identifier is not present in the input");

        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, input);

        Assert.Single(result.Spans);
        Assert.Equal(identifier, result.Spans[0].Text);
        Assert.Equal(start, result.Spans[0].CharacterStart);
        Assert.Equal(start + identifier.Length, result.Spans[0].CharacterEnd);

        var filtered = new FilterService().Filter(CreatePolicy(), "test", 0, input).FilteredText;
        Assert.Equal(input.Replace(identifier, "{{{REDACTED-ssn}}}"), filtered);
    }

    private static void AssertNotDetected(string input)
    {
        Assert.Empty(CreateFilter().Filter(CreatePolicy(), "test", 0, input).Spans);
    }

    [Theory]
    [InlineData("-")] // hyphen-minus, the ASCII control
    [InlineData("\u00AD")] // soft hyphen
    [InlineData("\u2010")] // hyphen
    [InlineData("\u2011")] // non-breaking hyphen
    [InlineData("\u2012")] // figure dash
    [InlineData("\u2013")] // en dash
    [InlineData("\u2014")] // em dash
    [InlineData("\u2015")] // horizontal bar
    [InlineData("\u2212")] // minus sign
    [InlineData("\uFE58")] // small em dash
    [InlineData("\uFE63")] // small hyphen-minus
    [InlineData("\uFF0D")] // fullwidth hyphen-minus
    public void Filter_AcceptsEveryHyphenSubstitute(string hyphen)
    {
        var identifier = "078" + hyphen + "05" + hyphen + "1120";
        AssertRedacted("The SSN is " + identifier + ".", identifier);
    }

    [Theory]
    [InlineData(" ")] // space
    [InlineData("\t")] // tab
    [InlineData("\u00A0")] // non-breaking space
    public void Filter_AcceptsHorizontalWhitespaceSeparators(string space)
    {
        var identifier = "078" + space + "05" + space + "1120";
        AssertRedacted("The SSN is " + identifier + ".", identifier);
    }

    [Fact]
    public void Filter_AcceptsWhitespaceFollowingAHyphen()
    {
        AssertRedacted("The SSN is 078-  05-  1120.", "078-  05-  1120");
    }

    [Theory]
    [InlineData("078-05-\n1120")] // wrapped after the second hyphen
    [InlineData("078-\n05-1120")] // wrapped after the first hyphen
    [InlineData("078-05-\r\n1120")] // CRLF break
    [InlineData("078-05-\n    1120")] // indented continuation line
    [InlineData("078-05- \n\t1120")] // horizontal space on either side of the break
    [InlineData("078\u201105\u2011\n1120")] // a hyphen substitute preceding the break
    public void Filter_DetectsIdentifierWrappedAcrossALineBreak(string identifier)
    {
        AssertRedacted("The SSN is " + identifier + ".", identifier);
    }

    [Theory]
    [InlineData("123\n45\n6789")] // three numbers on three lines: the former false positive
    [InlineData("123\r\n45\r\n6789")]
    [InlineData("123 45\n6789")] // a bare break is no separator even next to a valid one
    [InlineData("078-05-11\n20")] // the break falls inside a digit group
    [InlineData("078-\n\n05-1120")] // more than one line break
    [InlineData("078  05  1120")] // more than one horizontal space, and no hyphen
    [InlineData("078 - 05 - 1120")] // whitespace before the hyphen
    [InlineData("\uFF10\uFF17\uFF18\uFF0D\uFF10\uFF15\uFF0D\uFF11\uFF11\uFF12\uFF10")] // fullwidth digits
    [InlineData("\u0660\u0667\u0668-\u0660\u0665-\u0661\u0661\u0662\u0660")] // Arabic-Indic digits
    public void Filter_DoesNotDetectExcludedForms(string input)
    {
        AssertNotDetected("The SSN is " + input + ".");
    }

    [Fact]
    public void Filter_DetectsRepeatedIdentifiersInOneDocument()
    {
        const string input = "Primary 078-05-1120 on file; the wrapped copy reads 219\u201109-\n9999 instead.";

        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, input);

        Assert.Equal(2, result.Spans.Count);
        Assert.Contains(result.Spans, s => s.Text == "078-05-1120");
        Assert.Contains(result.Spans, s => s.Text == "219\u201109-\n9999");

        var filtered = new FilterService().Filter(CreatePolicy(), "test", 0, input).FilteredText;
        Assert.Equal(
            "Primary {{{REDACTED-ssn}}} on file; the wrapped copy reads {{{REDACTED-ssn}}} instead.",
            filtered);
    }

    [Fact]
    public void Filter_DetectsIdentifierSurroundedByNonAsciiText()
    {
        AssertRedacted("\u60A3\u8005\u306ESSN\u306F078\u201105\u20111120\u3067\u3059\u3002",
            "078\u201105\u20111120");
    }
}
