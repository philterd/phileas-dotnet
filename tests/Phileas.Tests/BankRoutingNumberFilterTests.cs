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
    [InlineData("Routing: 021000021")] // JPMorgan Chase
    [InlineData("ABA: 322271627")] // Wells Fargo
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
    [InlineData("Zip: 12345")] // 5 digits (not 9)
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
        var result = new FilterService().Filter(policy, "test", 0, "Wire routing: 021000021 for payment.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("021000021", result.FilteredText);
    }

    [Theory]
    [InlineData("Routing: 021000022")] // a valid routing number with its last digit changed
    [InlineData("Routing: 111000026")]
    [InlineData("Number 123456789")]
    public void Filter_DoesNotDetectANumberFailingTheAbaChecksum(string input)
    {
        Assert.Empty(CreateFilter().Filter(CreatePolicy(), "test", 0, input).Spans);
    }

    // An unformatted SSN, ITIN or SIN was claimed as a routing number at 0.95 when both filters were enabled.
    [Theory]
    [InlineData("\"ssn\": {}", "Number 123456789", FilterType.Ssn)]
    [InlineData("\"itin\": {}", "Number 912701234", FilterType.Itin)]
    [InlineData("\"canadaSin\": {}", "Number 271835464", FilterType.CanadaSin)]
    [InlineData("\"ssn\": {}", "Number 123456706", FilterType.BankRoutingNumber)] // passes the checksum
    [InlineData("\"itin\": {}", "Number 912701206", FilterType.BankRoutingNumber)] // passes the checksum
    [InlineData("\"ssn\": {}", "SSN 123456706", FilterType.Ssn)] // boosted to 0.95, a tie that SSN wins on filter order
    [InlineData("\"canadaSin\": {}", "Number 271800013", FilterType.BankRoutingNumber)] // passes Luhn and the checksum
    [InlineData("\"canadaSin\": {}", "SIN 271800013", FilterType.CanadaSin)] // boosted to 0.95, a tie the SIN wins
    [InlineData("\"ssn\": {}", "routing SSN 123456706", FilterType.BankRoutingNumber)] // 1.0 against 0.95
    public void FilterService_ANineDigitRunGoesToRoutingOnlyWhenItPassesTheChecksum(string other, string input,
        FilterType expected)
    {
        var policy = PolicySerializer.DeserializeFromJson(
            "{\"identifiers\": {\"bankRoutingNumber\": {}, " + other + "}}");
        var span = Assert.Single(new FilterService().Filter(policy, "test", 0, input).Spans);
        Assert.Equal(expected, span.FilterType);
    }
}