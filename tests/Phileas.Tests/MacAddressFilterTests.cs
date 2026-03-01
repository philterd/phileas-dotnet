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

public class MacAddressFilterTests
{
    private static MacAddressFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new MacAddressFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new MacAddressFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { MacAddress = new MacAddress() }
        };
    }

    [Theory]
    [InlineData("MAC: 00:1A:2B:3C:4D:5E")]
    [InlineData("Hardware: FF:FF:FF:FF:FF:FF")]
    [InlineData("Device: 00:00:00:00:00:00")]
    public void Filter_DetectsColonSeparatedMacAddress(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.MacAddress, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("MAC: 00-1A-2B-3C-4D-5E")]
    [InlineData("Device: A1-B2-C3-D4-E5-F6")]
    public void Filter_DetectsHyphenSeparatedMacAddress(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.MacAddress, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("No MAC here.")]
    [InlineData("IP: 192.168.1.1")]
    [InlineData("Serial: 12345678")]
    public void Filter_DoesNotDetectNonMacAddress(string input)
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
    public void FilterService_RedactsMacAddress()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { MacAddress = new MacAddress() }
        };
        var result = FilterService.Filter(policy, "test", 0, "Device MAC: 00:1A:2B:3C:4D:5E");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("00:1A:2B:3C:4D:5E", result.FilteredText);
    }
}
