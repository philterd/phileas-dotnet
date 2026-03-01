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

public class IbanCodeFilterTests
{
    private static IbanCodeFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new IbanCodeFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new IbanCodeFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { IbanCode = new IbanCode() }
        };
    }

    [Theory]
    [InlineData("IBAN: GB29NWBK60161331926819")]    // UK IBAN
    [InlineData("Account: DE89370400440532013000")] // German IBAN
    [InlineData("Bank: FR7614508059151234567890185")] // French IBAN
    public void Filter_DetectsIbanCode(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.IbanCode, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("No IBAN here.")]
    [InlineData("Account: 12345678")]
    [InlineData("Ref: ABC123")]
    public void Filter_DoesNotDetectNonIban(string input)
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
    public void FilterService_RedactsIban()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { IbanCode = new IbanCode() }
        };
        var result = FilterService.Filter(policy, "test", 0, "Wire to GB29NWBK60161331926819");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("GB29NWBK60161331926819", result.FilteredText);
    }
}
