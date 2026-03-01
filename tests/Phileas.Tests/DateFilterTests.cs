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
using Phileas.Model;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Regex;
using Phileas.Filters.Strategies.Rules;
using Phileas.Services;
using Xunit;

namespace Phileas.Tests;

public class DateFilterTests
{
    private static DateFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new DateFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new DateFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Date = new Date() }
        };
    }

    [Theory]
    [InlineData("DOB: 01/15/1990")]
    [InlineData("Date: 12-31-2000")]
    [InlineData("On 6.4.2020")]
    public void Filter_DetectsNumericDate(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.Date, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("Born on January 15, 1990")]
    [InlineData("Event on March 3, 2024")]
    [InlineData("Appointment: December 31 2025")]
    public void Filter_DetectsWrittenOutDate(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.Date, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("Visit on Jan. 5, 2023")]
    [InlineData("Due: Feb 28, 2022")]
    [InlineData("Filed: Oct 15 2021")]
    public void Filter_DetectsAbbreviatedMonthDate(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.Date, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("No date here.")]
    [InlineData("Call us at 12345")]
    public void Filter_DoesNotDetectNonDate(string input)
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
    public void FilterService_RedactsDate()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Date = new Date() }
        };
        var result = FilterService.Filter(policy, "test", 0, "DOB: 01/15/1990");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("01/15/1990", result.FilteredText);
    }
}
