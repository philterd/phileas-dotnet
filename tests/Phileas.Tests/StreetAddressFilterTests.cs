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
using Phileas.Filters.Rules.Regex.RegexFilters;
using Phileas.Filters.Strategies.Rules;
using Phileas.Services;
using Xunit;

namespace Phileas.Tests;

public class StreetAddressFilterTests
{
    private static StreetAddressFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new StreetAddressFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new StreetAddressFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { StreetAddress = new StreetAddress() }
        };
    }

    [Theory]
    [InlineData("Lives at 123 Main Street")]
    [InlineData("Office: 456 Oak Avenue")]
    [InlineData("Mailing: 789 Elm Blvd")]
    [InlineData("Address: 10 Sunset Drive")]
    [InlineData("HQ at 500 Park Road")]
    public void Filter_DetectsStreetAddress(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.StreetAddress, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("No address here.")]
    [InlineData("Chapter 4 summary")]
    public void Filter_DoesNotDetectNonAddress(string input)
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
    public void FilterService_RedactsStreetAddress()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { StreetAddress = new StreetAddress() }
        };
        var result = FilterService.Filter(policy, "test", 0, "Send mail to 123 Main Street.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("123 Main Street", result.FilteredText);
    }
}
