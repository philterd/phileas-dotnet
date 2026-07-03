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

public class StreetAddressFilterTests
{
    private static StreetAddressFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new StreetAddressFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new StreetAddressFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { StreetAddress = new StreetAddress() }
        };
    }

    [Theory]
    [InlineData("Lives at 123 Main Street")]
    [InlineData("Office: 456 Oak Avenue")]
    [InlineData("Mailing: 789 Elm Blvd")]
    [InlineData("Address: 10 Sunset Drive")]
    [InlineData("HQ at 500 Park Road")]
    public void Filter_DetectsStreetAddress(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.StreetAddress, result.Spans[0].FilterType);
    }

    [Theory]
    // Directionals (pre-name and post-suffix quadrant)
    [InlineData("123 N Main St")]
    [InlineData("123 North Main Street")]
    [InlineData("Ship to 123 Main St NW")]
    // Ordinal street names
    [InlineData("Reg: 123 5th Avenue")]
    [InlineData("At 42 42nd Street")]
    // Expanded street types
    [InlineData("Loc 10 Sunset Loop")]
    [InlineData("Site 200 Bear Pike")]
    [InlineData("5 Kings Crossing")]
    [InlineData("9 Queens Crescent")]
    [InlineData("12 Harbor Plaza")]
    [InlineData("34 Oak Point")]
    [InlineData("77 Rectory Mews")]
    // House-number range / letter suffix
    [InlineData("123-125 Main St")]
    [InlineData("123A Main Street")]
    // Saint / abbreviated street name
    [InlineData("100 St. Charles Avenue")]
    // PO boxes
    [InlineData("PO Box 1234")]
    [InlineData("Mail to P.O. Box 56")]
    [InlineData("Post Office Box 789")]
    public void Filter_DetectsExpandedFormats(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.StreetAddress, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("123 Main St Apt 4B", "Apt 4B")]
    [InlineData("456 Oak Ave, Suite 200", "Suite 200")]
    [InlineData("789 Elm Blvd Unit 12", "Unit 12")]
    [InlineData("12 Pine Rd #5", "#5")]
    [InlineData("Ship to 123 Main St NW", "NW")]
    public void Filter_SpanIncludesUnitOrDirectional(string input, string expectedTail)
    {
        // The secondary unit / trailing quadrant is folded into the redacted span, not left behind.
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Contains(expectedTail, result.Spans[0].Text);
    }

    [Theory]
    [InlineData("No address here.")]
    [InlineData("Chapter 4 summary")]
    [InlineData("Meeting at 3 PM today")]
    [InlineData("Section 5 covers everything")]
    [InlineData("We have 2 dogs and 3 cats")]
    [InlineData("PO Box without a number")]
    public void Filter_DoesNotDetectNonAddress(string input)
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
    public void FilterService_RedactsStreetAddress()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { StreetAddress = new StreetAddress() }
        };
        var result = new FilterService().Filter(policy, "test", 0, "Send mail to 123 Main Street.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("123 Main Street", result.FilteredText);
    }
}