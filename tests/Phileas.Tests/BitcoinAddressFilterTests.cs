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

public class BitcoinAddressFilterTests
{
    private static BitcoinAddressFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new BitcoinAddressFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new BitcoinAddressFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { BitcoinAddress = new BitcoinAddress() }
        };
    }

    [Theory]
    [InlineData("Send to 1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa")]   // P2PKH (starts with 1)
    [InlineData("Wallet: 3J98t1WpEZ73CNmQviecrnyiWrnqRhWNLy")]    // P2SH (starts with 3)
    public void Filter_DetectsLegacyBitcoinAddress(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.BitcoinAddress, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("Native segwit: bc1qar0srrr7xfkvy5l643lydnw9re59gtzzwf5mdq")]  // bech32
    [InlineData("Pay to: bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4")]
    public void Filter_DetectsBech32BitcoinAddress(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.BitcoinAddress, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("No wallet here.")]
    [InlineData("Email: test@example.com")]
    [InlineData("Short: abc123")]
    public void Filter_DoesNotDetectNonBitcoinAddress(string input)
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
    public void FilterService_RedactsBitcoinAddress()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { BitcoinAddress = new BitcoinAddress() }
        };
        var result = FilterService.Filter(policy, "test", 0, "Send BTC to 1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa");
        Assert.Contains("REDACTED", result.FilteredText);
    }
}
