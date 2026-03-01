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

public class TrackingNumberFilterTests
{
    private static TrackingNumberFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new TrackingNumberFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new TrackingNumberFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { TrackingNumber = new TrackingNumber() }
        };
    }

    [Theory]
    [InlineData("UPS: 1Z12345E0205271688")]      // UPS (1Z + 16 alphanumeric)
    [InlineData("Shipment 1Z999AA10123456784")]
    public void Filter_DetectsUpsTrackingNumber(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.TrackingNumber, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("FedEx: 449044304137821")]        // FedEx 15-digit
    [InlineData("Package: 798429808620")]          // 12-digit numeric tracking
    public void Filter_DetectsNumericTrackingNumber(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.TrackingNumber, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("No tracking here.")]
    [InlineData("SSN: 123-45-6789")]
    public void Filter_DoesNotDetectNonTrackingNumber(string input)
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
    public void FilterService_RedactsTrackingNumber()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { TrackingNumber = new TrackingNumber() }
        };
        var result = FilterService.Filter(policy, "test", 0, "Package UPS: 1Z12345E0205271688 has shipped.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("1Z12345E0205271688", result.FilteredText);
    }
}
