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

public class BankRoutingNumberFilterTests
{
    private static BankRoutingNumberFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new BankRoutingNumberFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new BankRoutingNumberFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { BankRoutingNumber = new BankRoutingNumber() }
        };
    }

    [Theory]
    [InlineData("Routing: 021000021")]   // JPMorgan Chase
    [InlineData("ABA: 322271627")]       // Wells Fargo
    [InlineData("Routing number 111000025")]
    public void Filter_DetectsBankRoutingNumber(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.BankRoutingNumber, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("No routing here.")]
    [InlineData("Zip: 12345")]            // 5 digits (not 9)
    [InlineData("Too long: 1234567890")] // 10 digits
    public void Filter_DoesNotDetectNonRoutingNumber(string input)
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
    public void FilterService_RedactsBankRoutingNumber()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { BankRoutingNumber = new BankRoutingNumber() }
        };
        var result = FilterService.Filter(policy, "test", 0, "Wire routing: 021000021 for payment.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("021000021", result.FilteredText);
    }
}
