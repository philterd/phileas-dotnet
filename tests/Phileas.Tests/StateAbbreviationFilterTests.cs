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

public class StateAbbreviationFilterTests
{
    private static StateAbbreviationFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new StateAbbreviationFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new StateAbbreviationFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { StateAbbreviation = new StateAbbreviation() }
        };
    }

    [Theory]
    [InlineData("City, CA 90210")]
    [InlineData("Austin, TX")]
    [InlineData("Boston, MA")]
    [InlineData("Miami, FL")]
    [InlineData("Seattle, WA")]
    public void Filter_DetectsStateAbbreviation(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.StateAbbreviation, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("Washington DC")]
    [InlineData("Puerto Rico PR")]
    public void Filter_DetectsTerritoriesAndDC(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.StateAbbreviation, result.Spans[0].FilterType);
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
    public void FilterService_RedactsStateAbbreviation()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { StateAbbreviation = new StateAbbreviation() }
        };
        var result = new FilterService().Filter(policy, "test", 0, "Address: 123 Main St, Springfield, IL 62701");
        Assert.Contains("REDACTED", result.FilteredText);
    }
}
