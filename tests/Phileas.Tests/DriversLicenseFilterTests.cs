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
using Phileas.Model.Filtering;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Regex;
using Phileas.Strategies.Rules;
using Xunit;

namespace Phileas.Tests;

public class DriversLicenseFilterTests
{
    private static DriversLicenseFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new DriversLicenseFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new DriversLicenseFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { DriversLicense = new DriversLicense() }
        };
    }

    [Theory]
    [InlineData("DL: A1234567")]    // Letter + 7 digits
    [InlineData("License: B9876543")]
    public void Filter_DetectsLetterSevenDigitLicense(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.DriversLicenseNumber, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("DL: AB123456")]    // 2 letters + 6 digits
    [InlineData("License: XY654321")]
    public void Filter_DetectsTwoLetterSixDigitLicense(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.DriversLicenseNumber, result.Spans[0].FilterType);
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
    public void FilterService_RedactsDriversLicense()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { DriversLicense = new DriversLicense() }
        };
        var result = FilterService.Filter(policy, "test", 0, "DL Number: A1234567");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("A1234567", result.FilteredText);
    }
}
