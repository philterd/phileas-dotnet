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

public class IpAddressFilterTests
{
    private static IpAddressFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new IpAddressFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new IpAddressFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { IpAddress = new IpAddress() }
        };
    }

    [Theory]
    [InlineData("Server at 192.168.1.1 is down.")]
    [InlineData("Connect to 10.0.0.1")]
    [InlineData("The address is 255.255.255.0")]
    [InlineData("Loopback: 127.0.0.1")]
    [InlineData("Broadcast: 0.0.0.0")]
    public void Filter_DetectsIPv4Address(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.IpAddress, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("IPv6: 2001:0db8:85a3:0000:0000:8a2e:0370:7334")]
    [InlineData("Address: fe80:0000:0000:0000:0202:b3ff:fe1e:8329")]
    public void Filter_DetectsIPv6Address(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.IpAddress, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("No IP here.")]
    [InlineData("Version 1.2 released")]
    [InlineData("Price: $1.99")]
    public void Filter_DoesNotDetectNonIPAddress(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void Filter_DetectsMultipleIPAddresses()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, "From 10.0.0.1 to 10.0.0.2");
        Assert.Equal(2, result.Spans.Count);
    }

    [Fact]
    public void Filter_ReturnsCorrectSpanPositions()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        const string input = "IP: 192.168.0.1 end";
        var result = filter.Filter(policy, "test", 0, input);
        Assert.Single(result.Spans);
        Assert.Equal("192.168.0.1", result.Spans[0].Text);
        Assert.Equal(4, result.Spans[0].CharacterStart);
        Assert.Equal(15, result.Spans[0].CharacterEnd);
    }

    [Fact]
    public void Filter_EmptyInput_ReturnsNoSpans()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, string.Empty);
        Assert.Empty(result.Spans);
    }
}
