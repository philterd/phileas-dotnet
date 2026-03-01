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
using Xunit;

namespace Phileas.Tests;

public class CurrencyFilterTests
{
    private static CurrencyFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new CurrencyFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new CurrencyFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Currency = new Currency() }
        };
    }

    [Theory]
    [InlineData("Total: $1,200.50")]
    [InlineData("Price: $99")]
    [InlineData("Revenue: $5 million")]
    [InlineData("Cost: $1.5 billion")]
    public void Filter_DetectsDollarAmount(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.Currency, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("Amount: 1000 USD")]
    [InlineData("Cost: 500 EUR")]
    [InlineData("Transfer: 250 GBP")]
    [InlineData("Price: 1500 JPY")]
    public void Filter_DetectsIsoCurrencyCode(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.Currency, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("No money here.")]
    [InlineData("Just a number: 12345")]
    public void Filter_DoesNotDetectNonCurrency(string input)
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
    public void FilterService_RedactsCurrency()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Currency = new Currency() }
        };
        var result = FilterService.Filter(policy, "test", 0, "Salary: $75,000.00 per year.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("$75,000.00", result.FilteredText);
    }
}
