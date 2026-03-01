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
}