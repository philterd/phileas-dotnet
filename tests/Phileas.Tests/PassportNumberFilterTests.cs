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
using Phileas.Filters.Regex;
using Phileas.Strategies.Rules;
using Xunit;

namespace Phileas.Tests;

public class PassportNumberFilterTests
{
    private static PassportNumberFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new PassportNumberFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new PassportNumberFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { PassportNumber = new PassportNumber() }
        };
    }

    [Theory]
    [InlineData("Passport: A12345678")]   // 1 letter + 8 digits
    [InlineData("Travel doc: B98765432")]
    [InlineData("Passport No: AB123456")]  // 2 letters + 6 digits
    public void Filter_DetectsPassportNumber(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.PassportNumber, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("No passport here.")]
    [InlineData("Name: John Smith")]
    public void Filter_DoesNotDetectNonPassportNumber(string input)
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
    public void FilterService_RedactsPassportNumber()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { PassportNumber = new PassportNumber() }
        };
        var result = FilterService.Filter(policy, "test", 0, "Passport: A12345678 expires next year.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("A12345678", result.FilteredText);
    }
}
