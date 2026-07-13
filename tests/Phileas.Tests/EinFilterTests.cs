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
}
