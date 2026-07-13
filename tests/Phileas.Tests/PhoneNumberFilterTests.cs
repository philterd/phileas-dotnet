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
using Phileas.Filters.Rules;
using Phileas.Filters.Strategies.Rules;
using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

public class PhoneNumberFilterTests
{
    private static PhoneNumberFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new PhoneNumberFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new PhoneNumberFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { PhoneNumber = new PhoneNumber() }
        };
    }

    [Theory]
    [InlineData("Call 555-867-5309")]
    [InlineData("Phone: (555) 867-5309")]
    [InlineData("Reach us at 555.867.5309")]
    [InlineData("+1 555 867 5309")]
    public void Filter_DetectsPhoneNumber(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
    }

    [Theory]
    [InlineData("No phone here.")]
    [InlineData("Just a number: 123")]
    [InlineData("ZIP code: 12345")]
    public void Filter_DoesNotDetectNonPhoneNumber(string input)
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
    public void Filter_ReturnsCorrectFilterType()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, "Call 555-867-5309");
        Assert.Equal(FilterType.PhoneNumber, result.Spans[0].FilterType);
    }

    [Fact]
    public void Filter_DetectsMultiplePhoneNumbers()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, "Call 555-867-5309 or 555-123-4567");
        Assert.Equal(2, result.Spans.Count);
    }

    // The international-detection gap this filter closes (issue #55): +-prefixed numbers from any region are
    // found regardless of the default US region, matching the Java libphonenumber-backed filter.
    [Theory]
    [InlineData("Call +44 20 7946 0958 today", "+44 20 7946 0958")]
    [InlineData("Ring +33 1 42 68 53 00 now", "+33 1 42 68 53 00")]
    [InlineData("Mobile +91 98765 43210 please", "+91 98765 43210")]
    [InlineData("Office +49 30 901820 ext", "+49 30 901820")]
    public void Filter_DetectsInternationalPhoneNumbers(string input, string expected)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();

        var result = filter.Filter(policy, "test", 0, input);

        var span = Assert.Single(result.Spans);
        Assert.Equal(expected, span.Text);
    }

    // Confidence tiers mirror the Java PhoneNumberRulesFilter: a cleanly NANP-formatted match is 0.95;
    // other found numbers are 0.75 (longer than 14 chars) or 0.60.
    [Theory]
    [InlineData("Call 555-123-4567", 0.95)]
    [InlineData("Phone: (555) 867-5309", 0.95)]
    [InlineData("+1 555 867 5309", 0.95)]
    [InlineData("Call +44 20 7946 0958 today", 0.75)]
    [InlineData("Office +49 30 901820 ext", 0.60)]
    public void Filter_AssignsJavaParityConfidence(string input, double expectedConfidence)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();

        var result = filter.Filter(policy, "test", 0, input);

        var span = Assert.Single(result.Spans);
        Assert.Equal(expectedConfidence, span.Confidence, 3);
    }

    [Theory]
    [InlineData("Reach the London office at +44 20 7946 0958.")]
    [InlineData("Ring +33 1 42 68 53 00 now")]
    [InlineData("Mobile +91 98765 43210 please")]
    [InlineData("Office +49 30 901820 ext")]
    [InlineData("Reach us at 555-123-4567.")]
    public void FilterService_RedactsThroughTheFullPipeline(string input)
    {
        // Through FilterService (the pipeline the default policy and Philter Desktop use), the number is
        // redacted rather than shipped in the clear.
        var result = new FilterService().Filter(CreatePolicy(), "ctx", 0, input);

        var span = Assert.Single(result.Spans);
        Assert.DoesNotContain(span.Text, result.FilteredText);
    }

    [Fact]
    public void Filter_SetsSpanOffsetsToTheMatchedNumber()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        const string input = "Call 555-123-4567 today";

        var span = Assert.Single(filter.Filter(policy, "test", 0, input).Spans);

        // The offsets must bound exactly the number, so redaction covers the right characters.
        var start = input.IndexOf("555-123-4567", StringComparison.Ordinal);
        Assert.Equal(start, span.CharacterStart);
        Assert.Equal(start + "555-123-4567".Length, span.CharacterEnd);
        Assert.Equal("555-123-4567", input[span.CharacterStart..span.CharacterEnd]);
    }

    [Fact]
    public void Filter_ExcludesIgnoredNumbers()
    {
        // A number in the ignored set is dropped (the same mark-then-post-filter path as the Java filter).
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new PhoneNumberFilterStrategy() })
            .WithIgnored(new HashSet<string> { "555-123-4567" })
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        var filter = new PhoneNumberFilter(config);

        var result = filter.Filter(CreatePolicy(), "test", 0, "Call 555-123-4567 or 555-867-5309");

        // Only the non-ignored number survives.
        var span = Assert.Single(result.Spans);
        Assert.Equal("555-867-5309", span.Text);
    }
}