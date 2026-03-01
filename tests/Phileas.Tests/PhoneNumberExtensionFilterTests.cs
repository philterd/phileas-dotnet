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

public class PhoneNumberExtensionFilterTests
{
    private static PhoneNumberExtensionFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new PhoneNumberExtensionFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new PhoneNumberExtensionFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { PhoneNumberExtension = new PhoneNumberExtension() }
        };
    }

    [Theory]
    [InlineData("Call ext. 1234")]
    [InlineData("Dial extension 5678")]
    [InlineData("Contact x200")]
    [InlineData("Reach us at ext 9")]
    public void Filter_DetectsPhoneExtension(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.PhoneNumberExtension, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("No extension here.")]
    [InlineData("Call 555-867-5309")]
    public void Filter_DoesNotDetectNonExtension(string input)
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
    public void FilterService_RedactsPhoneExtension()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { PhoneNumberExtension = new PhoneNumberExtension() }
        };
        var result = FilterService.Filter(policy, "test", 0, "Call us at ext. 4200 for support.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("ext. 4200", result.FilteredText);
    }
}
