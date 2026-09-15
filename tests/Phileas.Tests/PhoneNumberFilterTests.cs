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
    private static FilterConfiguration CreateConfiguration()
    {
        return new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new PhoneNumberFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
    }

    /// <summary>A filter left on the default region, built through the configuration-only constructor.</summary>
    private static PhoneNumberFilter CreateFilter()
    {
        return new PhoneNumberFilter(CreateConfiguration());
    }

    private static PhoneNumberFilter CreateFilter(params string[] regions)
    {
        return new PhoneNumberFilter(CreateConfiguration(), regions);
    }

    private static PhileasPolicy CreatePolicy(params string[] regions)
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                PhoneNumber = new PhoneNumber { Region = regions.Length > 0 ? regions.ToList() : null }
            }
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

    // The policy's region property (issue #53) sets the region(s) used to read numbers written without a
    // "+" country code. These mirror the Java PhoneNumberRulesFilterTest region cases.
    [Fact]
    public void Filter_DetectsNationalFormatNumbersForTheConfiguredRegion()
    {
        var filter = CreateFilter("GB");

        var result = filter.Filter(CreatePolicy("GB"), "test", 0, "the number is 020 7946 0958.");

        var span = Assert.Single(result.Spans);
        Assert.Equal("020 7946 0958", span.Text);
    }

    [Fact]
    public void Filter_DoesNotDetectForeignNationalFormatNumbersUnderTheDefaultRegion()
    {
        // Under the default US region the UK number is not read as a phone number: only the 7-digit
        // fragment "020 7946" is extracted (possible-but-invalid under the NANP), so the number itself
        // is missed. Java finds nothing at all for this input; libphonenumber-csharp's matcher is more
        // permissive about 7-digit candidates, a port difference that predates region support.
        var filter = CreateFilter();

        var span = Assert.Single(filter.Filter(CreatePolicy(), "test", 0, "the number is 020 7946 0958.").Spans);

        Assert.Equal("020 7946", span.Text);
    }

    [Fact]
    public void Filter_DetectsNationalFormatNumbersFromEveryConfiguredRegion()
    {
        var filter = CreateFilter("US", "GB", "FR");

        var result = filter.Filter(CreatePolicy("US", "GB", "FR"), "test", 0,
            "call 123-456-7890 or 020 7946 0958 or 01 42 68 53 00.");

        // One number per region, in document order. The UK number is kept whole: the US scan also yields the
        // 7-digit fragment "020 7946", and de-duplication prefers the valid, longer match over it.
        Assert.Equal(new[] { "123-456-7890", "020 7946 0958", "01 42 68 53 00" },
            result.Spans.Select(span => span.Text).ToArray());
    }

    [Fact]
    public void Filter_FallsBackToTheDefaultRegionForAnEmptyRegionList()
    {
        // An empty array (or a blank code) is treated as "not set" rather than as "no regions", which would
        // otherwise scan nothing and silently detect only "+"-prefixed numbers.
        var filter = CreateFilter();
        var empty = CreateFilter(Array.Empty<string>());
        var blank = CreateFilter(" ");

        const string input = "Call 555-123-4567";

        Assert.Equal(Assert.Single(filter.Filter(CreatePolicy(), "test", 0, input).Spans).Text,
            Assert.Single(empty.Filter(CreatePolicy(), "test", 0, input).Spans).Text);
        Assert.Equal("555-123-4567", Assert.Single(blank.Filter(CreatePolicy(), "test", 0, input).Spans).Text);
    }

    [Fact]
    public void Filter_WithAnUnrecognizedRegionDetectsOnlyInternationalNumbers()
    {
        // libphonenumber matches region codes exactly (uppercase ISO 3166-1 alpha-2), so "gb" and "ZZ" are
        // both unknown regions: national-format numbers are then unreachable and only "+"-prefixed numbers
        // are found. Verified to be the same in the Java port, which is given the region code unchanged too.
        const string input = "call 555-123-4567 or +44 20 7946 0958 now";

        foreach (var region in new[] { "gb", "ZZ" })
        {
            var result = CreateFilter(region).Filter(CreatePolicy(region), "test", 0, input);

            Assert.Equal("+44 20 7946 0958", Assert.Single(result.Spans).Text);
        }
    }

    [Fact]
    public void Filter_DetectsInternationalNumbersRegardlessOfRegion()
    {
        // A "+"-prefixed number is detected even though the configured region is GB.
        var filter = CreateFilter("GB");

        var result = filter.Filter(CreatePolicy("GB"), "test", 0, "the number is +1 202-555-0182.");

        var span = Assert.Single(result.Spans);
        Assert.Equal("+1 202-555-0182", span.Text);
    }

    [Fact]
    public void Filter_DeduplicatesAcrossRegions()
    {
        // A single "+"-prefixed number is found under every region; the merged result must not double it.
        var filter = CreateFilter("US", "GB", "FR");

        var result = filter.Filter(CreatePolicy("US", "GB", "FR"), "test", 0, "the number is +1 202-555-0182.");

        var span = Assert.Single(result.Spans);
        Assert.Equal("+1 202-555-0182", span.Text);
    }

    [Fact]
    public void FilterService_UsesTheRegionFromThePolicy()
    {
        // The region reaches the filter through FilterService, not just the filter's own constructor.
        const string input = "the number is 020 7946 0958.";

        var withRegion = new FilterService().Filter(CreatePolicy("GB"), "ctx", 0, input);
        var withDefault = new FilterService().Filter(CreatePolicy(), "ctx", 0, input);

        Assert.Equal("020 7946 0958", Assert.Single(withRegion.Spans).Text);
        Assert.Equal("020 7946", Assert.Single(withDefault.Spans).Text);
    }
}