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

public class VinFilterTests
{
    private static VinFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new VinFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new VinFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Vin = new Vin() }
        };
    }

    [Theory]
    [InlineData("VIN: 1HGBH41JXMN109186")]   // Honda VIN example
    [InlineData("Vehicle: 2T1BURHE0JC043821")] // Toyota VIN example
    [InlineData("Car VIN 4T1BF3EK9AU118018")]  // Another valid VIN
    public void Filter_DetectsVin(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.Vin, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("No VIN here.")]
    [InlineData("Short: ABC123")]
    [InlineData("Too long: 1HGBH41JXMN1091860000")]
    public void Filter_DoesNotDetectNonVin(string input)
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
    public void FilterService_RedactsVin()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Vin = new Vin() }
        };
        var result = new FilterService().Filter(policy, "test", 0, "VIN: 1HGBH41JXMN109186 on file.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("1HGBH41JXMN109186", result.FilteredText);
    }
}
