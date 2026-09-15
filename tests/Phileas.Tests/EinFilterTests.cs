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
using EinPolicyStrategy = Phileas.Policy.Filters.Strategies.EinFilterStrategy;

namespace Phileas.Tests;

public class EinFilterTests
{
    private static EinFilter CreateFilter(bool onlyValidPrefixes = false)
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new EinFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new EinFilter(config, onlyValidPrefixes);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Ein = new Ein() }
        };
    }

    [Theory]
    [InlineData("EIN: 12-3456789")]
    [InlineData("Employer ID 20-1234567 on the W-9")]
    public void Filter_DetectsEin(string input)
    {
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, input);
        Assert.NotEmpty(result.Spans);
    }

    [Fact]
    public void Filter_ReturnsCorrectFilterType()
    {
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, "EIN: 12-3456789");
        Assert.Equal(FilterType.Ein, result.Spans[0].FilterType);
    }

    [Fact]
    public void Filter_ReturnsCorrectSpanPositions()
    {
        const string input = "EIN: 12-3456789 end";
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, input);
        Assert.Single(result.Spans);
        Assert.Equal("12-3456789", result.Spans[0].Text);
        Assert.Equal(5, result.Spans[0].CharacterStart);
        Assert.Equal(15, result.Spans[0].CharacterEnd);
    }

    [Theory]
    [InlineData("Number 123456789 here")] // Bare nine-digit run is ambiguous; not claimed as EIN.
    [InlineData("SSN: 123-45-6789")] // SSN hyphen positions (NNN-NN-NNNN), not EIN (NN-NNNNNNN).
    public void Filter_DoesNotDetectNonEinShapes(string input)
    {
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, input);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void Filter_EmptyInput_ReturnsNoSpans()
    {
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, string.Empty);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void Filter_NoEinInText_ReturnsNoSpans()
    {
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, "No sensitive data here.");
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void Filter_OnlyValidPrefixesOff_KeepsUnissuedPrefix()
    {
        // 07 is not an issued IRS prefix, but the default (off) matches any EIN-formatted value.
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, "EIN: 07-1234567");
        Assert.Single(result.Spans);
    }

    [Fact]
    public void Filter_OnlyValidPrefixesOn_KeepsIssuedPrefix()
    {
        var result = CreateFilter(true).Filter(CreatePolicy(), "test", 0, "EIN: 12-3456789");
        Assert.Single(result.Spans);
    }

    [Theory]
    [InlineData("EIN: 07-1234567")] // 07 is not issued.
    [InlineData("EIN: 00-1234567")] // 00 is not issued.
    public void Filter_OnlyValidPrefixesOn_DropsUnissuedPrefix(string input)
    {
        var result = CreateFilter(true).Filter(CreatePolicy(), "test", 0, input);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void FilterService_AppliesStrategyToEinSpans()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                Ein = new Ein
                {
                    Strategies = new List<EinPolicyStrategy>
                    {
                        new() { Strategy = AbstractFilterStrategy.Mask, MaskCharacter = "*" }
                    }
                }
            }
        };

        var result = new FilterService().Filter(policy, "test", 0, "EIN: 12-3456789");
        Assert.Single(result.Spans);
        Assert.Equal(FilterType.Ein, result.Spans[0].FilterType);
        Assert.DoesNotContain("12-3456789", result.FilteredText);
        Assert.Contains("**********", result.FilteredText);
    }

    [Fact]
    public void FilterService_OnlyValidPrefixesFromPolicy_DropsUnissuedPrefix()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Ein = new Ein { OnlyValidPrefixes = true } }
        };

        var result = new FilterService().Filter(policy, "test", 0, "EIN: 07-1234567");
        Assert.Empty(result.Spans);
        Assert.Equal("EIN: 07-1234567", result.FilteredText);
    }

    // -------------------------------------------------------------------------
    // Separators, digits and boundaries, shared with the SSN filter.
    // Every value below is synthetic. See philterd/phileas-dotnet#95.
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
        Assert.Equal(input.Replace(identifier, "{{{REDACTED-ein}}}"), filtered);
    }

    [Theory]
    [InlineData("\u002D")] // hyphen-minus, the ASCII control
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
        var identifier = "12" + hyphen + "3456789";
        AssertRedacted("The EIN is " + identifier + ".", identifier);
    }

    [Fact]
    public void Filter_AcceptsWhitespaceFollowingTheHyphen()
    {
        AssertRedacted("The EIN is 12-  3456789.", "12-  3456789");
    }

    [Theory]
    [InlineData("12-\n3456789")] // wrapped after the hyphen
    [InlineData("12-\r\n3456789")] // CRLF break
    [InlineData("12-\n    3456789")] // indented continuation line
    [InlineData("12- \n\t3456789")] // horizontal space on either side of the break
    [InlineData("12\u2011\n3456789")] // a hyphen substitute preceding the break
    public void Filter_DetectsIdentifierWrappedAcrossALineBreak(string identifier)
    {
        AssertRedacted("The EIN is " + identifier + ".", identifier);
    }

    [Theory]
    [InlineData("\uFF11\uFF12-\uFF13\uFF14\uFF15\uFF16\uFF17\uFF18\uFF19")] // fullwidth digits
    [InlineData("\u0661\u0662-\u0663\u0664\u0665\u0666\u0667\u0668\u0669")] // Arabic-Indic digits
    [InlineData("\u0967\u0968-\u0969\u096A\u096B\u096C\u096D\u096E\u096F")] // Devanagari digits
    public void Filter_DoesNotDetectNonAsciiDigits(string input)
    {
        Assert.Empty(CreateFilter().Filter(CreatePolicy(), "test", 0, "The EIN is " + input + ".").Spans);
    }

    [Theory]
    [InlineData("x 123-45-6789123-45-6789 y")] // a fragment straddling two run-on SSNs
    [InlineData("x -12-3456789- y")] // a hyphen on either side makes it part of a longer token
    [InlineData("x 12-34567891 y")] // eight digits after the hyphen
    [InlineData("x 123456789 y")] // no hyphen at all
    [InlineData("x 12\n3456789 y")] // a bare line break is not a separator
    public void Filter_DoesNotDetectAcrossBoundaries(string input)
    {
        Assert.Empty(CreateFilter().Filter(CreatePolicy(), "test", 0, input).Spans);
    }

    [Fact]
    public void Filter_OnlyValidPrefixesOn_KeepsIssuedPrefixOnAWrappedMatch()
    {
        // The prefix check reads the first two characters of the span, which stay the digits
        // however the identifier is separated.
        var result = CreateFilter(true).Filter(CreatePolicy(), "test", 0, "EIN: 12-\n3456789");

        Assert.Single(result.Spans);
        Assert.Equal("12-\n3456789", result.Spans[0].Text);
    }

    [Fact]
    public void Filter_OnlyValidPrefixesOn_DropsUnissuedPrefixOnAUnicodeHyphenMatch()
    {
        // 07 is not an issued prefix.
        Assert.Empty(CreateFilter(true).Filter(CreatePolicy(), "test", 0, "EIN: 07\u20113456789").Spans);
    }

    [Fact]
    public void Filter_OnlyValidPrefixesOff_KeepsUnissuedPrefixOnAUnicodeHyphenMatch()
    {
        AssertRedacted("EIN: 07\u20113456789.", "07\u20113456789");
    }

    [Fact]
    public void Filter_DetectsAnEinAndAnSsnInOneDocumentWithoutOverlap()
    {
        const string input = "EIN 12-3456789 and SSN 078-05-1120 on file.";

        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Ein = new Ein(), Ssn = new Ssn() }
        };

        var result = new FilterService().Filter(policy, "test", 0, input);

        Assert.Equal("EIN {{{REDACTED-ein}}} and SSN {{{REDACTED-ssn}}} on file.", result.FilteredText);
        Assert.Contains(result.Spans, s => s.FilterType == FilterType.Ein && s.Text == "12-3456789");
        Assert.Contains(result.Spans, s => s.FilterType == FilterType.Ssn && s.Text == "078-05-1120");
    }
}
