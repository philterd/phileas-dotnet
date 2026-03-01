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
    public void FilterStrategy_ShiftDate_NumericFormat_ShiftsByYears()
    {
        var strategy = new DateFilterStrategy { Strategy = "SHIFT_DATE", Years = 1 };
        var result = strategy.GetReplacement("ctx", "01/15/1990", [], 0.9, null, null, null, null);
        Assert.Equal("1/15/1991", result.Value);
        Assert.True(result.Applied);
    }

    [Fact]
    public void FilterStrategy_ShiftDate_NumericFormat_ShiftsByDays()
    {
        var strategy = new DateFilterStrategy { Strategy = "SHIFT_DATE", Days = 10 };
        var result = strategy.GetReplacement("ctx", "01/15/1990", [], 0.9, null, null, null, null);
        Assert.Equal("1/25/1990", result.Value);
        Assert.True(result.Applied);
    }

    [Fact]
    public void FilterStrategy_ShiftDate_NumericFormat_ShiftsByMonths()
    {
        var strategy = new DateFilterStrategy { Strategy = "SHIFT_DATE", Months = 2 };
        var result = strategy.GetReplacement("ctx", "01/15/1990", [], 0.9, null, null, null, null);
        Assert.Equal("3/15/1990", result.Value);
        Assert.True(result.Applied);
    }

    [Fact]
    public void FilterStrategy_ShiftDate_NumericFormat_DashSeparator()
    {
        var strategy = new DateFilterStrategy { Strategy = "SHIFT_DATE", Years = -1 };
        var result = strategy.GetReplacement("ctx", "12-31-2000", [], 0.9, null, null, null, null);
        Assert.Equal("12-31-1999", result.Value);
        Assert.True(result.Applied);
    }

    [Fact]
    public void FilterStrategy_ShiftDate_FullMonthName_ShiftsByYears()
    {
        var strategy = new DateFilterStrategy { Strategy = "SHIFT_DATE", Years = 1 };
        var result = strategy.GetReplacement("ctx", "January 15, 1990", [], 0.9, null, null, null, null);
        Assert.Equal("January 15, 1991", result.Value);
        Assert.True(result.Applied);
    }

    [Fact]
    public void FilterStrategy_ShiftDate_DayMonthYear_ShiftsByMonths()
    {
        var strategy = new DateFilterStrategy { Strategy = "SHIFT_DATE", Months = 1 };
        var result = strategy.GetReplacement("ctx", "15 January 1990", [], 0.9, null, null, null, null);
        Assert.Equal("15 February 1990", result.Value);
        Assert.True(result.Applied);
    }

    [Fact]
    public void FilterStrategy_ShiftDate_AbbreviatedMonthWithDot_ShiftsByYears()
    {
        var strategy = new DateFilterStrategy { Strategy = "SHIFT_DATE", Years = 1 };
        var result = strategy.GetReplacement("ctx", "Jan. 5, 2023", [], 0.9, null, null, null, null);
        Assert.Equal("Jan. 5, 2024", result.Value);
        Assert.True(result.Applied);
    }

    [Fact]
    public void FilterStrategy_ShiftDate_AbbreviatedMonthNoComma_ShiftsByMonths()
    {
        var strategy = new DateFilterStrategy { Strategy = "SHIFT_DATE", Months = 1 };
        var result = strategy.GetReplacement("ctx", "Feb 28 2022", [], 0.9, null, null, null, null);
        Assert.Equal("Mar 28 2022", result.Value);
        Assert.True(result.Applied);
    }

    [Fact]
    public void FilterStrategy_ShiftDate_ZeroShift_ReturnsReformattedDate()
    {
        var strategy = new DateFilterStrategy { Strategy = "SHIFT_DATE" };
        var result = strategy.GetReplacement("ctx", "01/15/1990", [], 0.9, null, null, null, null);
        // Zero shift still reformats; month/day leading zeros are not preserved.
        Assert.Equal("1/15/1990", result.Value);
    }

    [Fact]
    public void FilterService_ShiftDate_ProducesShiftedDate()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                Date = new Date
                {
                    Strategies = new List<Phileas.Policy.Filters.Strategies.DateFilterStrategy>
                    {
                        new() { Strategy = "SHIFT_DATE", Years = 1 }
                    }
                }
            }
        };
        var result = new FilterService().Filter(policy, "test", 0, "DOB: 01/15/1990");
        Assert.DoesNotContain("01/15/1990", result.FilteredText);
        Assert.Contains("1991", result.FilteredText);
    }

    [Fact]
    public void FilterService_RedactsDate()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Date = new Date() }
        };
        var result = new FilterService().Filter(policy, "test", 0, "DOB: 01/15/1990");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("01/15/1990", result.FilteredText);
    }
}