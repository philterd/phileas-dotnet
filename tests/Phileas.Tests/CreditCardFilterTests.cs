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

public class CreditCardFilterTests
{
    private static CreditCardFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new CreditCardFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new CreditCardFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { CreditCard = new CreditCard() }
        };
    }

    [Theory]
    [InlineData("Visa: 4111111111111111")]        // Visa 16-digit
    [InlineData("MC: 5500005555555559")]           // Mastercard
    [InlineData("Amex: 378282246310005")]          // Amex 15-digit
    [InlineData("Discover: 6011111111111117")]     // Discover
    public void Filter_DetectsCreditCardNumber(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.CreditCard, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("Card: 1234 5678 9012 3456")]     // formatted with spaces
    [InlineData("Card: 1234-5678-9012-3456")]     // formatted with hyphens
    public void Filter_DetectsFormattedCreditCard(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.CreditCard, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("No card here.")]
    [InlineData("Invoice #12345")]
    [InlineData("ZIP: 12345")]
    public void Filter_DoesNotDetectNonCreditCard(string input)
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
    public void FilterService_RedactsCreditCard()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { CreditCard = new CreditCard() }
        };
        var result = FilterService.Filter(policy, "test", 0, "Card: 4111111111111111 is on file.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("4111111111111111", result.FilteredText);
    }
}
