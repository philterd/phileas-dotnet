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
    [InlineData("UPS: 1Z12345E0205271688")] // UPS (1Z + 16 alphanumeric)
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
    [InlineData("FedEx: 449044304137821")] // FedEx 15-digit
    [InlineData("Package: 798429808620")] // 12-digit numeric tracking
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
        var result = new FilterService().Filter(policy, "test", 0, "Package UPS: 1Z12345E0205271688 has shipped.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("1Z12345E0205271688", result.FilteredText);
    }

    // --- No partial spans: philterd/phileas-dotnet#156 ----------------------------------------------

    private static TextFilterResult FilterThroughService(string number, bool allowSpaces = false)
    {
        var policy = new PhileasPolicy
        {
            Name = "t",
            Identifiers = new Identifiers { TrackingNumber = new TrackingNumber { AllowSpaces = allowSpaces } }
        };
        return new FilterService().Filter(policy, "ctx", 0, $"Ship {number} today");
    }

    // Either the whole number is one span or there is no span: never a span over only part of it.
    private static void AssertRedactedInFullOrNotAtAll(string number, TextFilterResult result)
    {
        Assert.All(result.Spans, span => Assert.Equal(number, span.Text));
        Assert.True(
            result.FilteredText == $"Ship {number} today" || result.FilteredText == "Ship {{{REDACTED-tracking-number}}} today",
            result.FilteredText);
    }

    [Theory]
    [InlineData("7012345678901234")]
    [InlineData("1234567890123456")]
    [InlineData("12345678901234567890123")]
    [InlineData("940010000000000000000000")]
    [InlineData("1Z999AA10123456784X")]
    [InlineData("1Z999AA101234567845")]
    [InlineData("12345678901234567890123456")]
    [InlineData("1234567890123456789012345678")]
    [InlineData("123456789012345678901234567890")]
    [InlineData("1234567890123456789012345678901234")]
    public void NumberLongerThanAPattern_IsNotPartlyRedacted(string number)
    {
        AssertRedactedInFullOrNotAtAll(number, FilterThroughService(number));
    }

    [Theory]
    [InlineData("1Z999AA101234567845", false)] // UPS: 1Z + 17
    [InlineData("1Z999AA101234567845", true)]
    [InlineData("12345678901234567890123", false)] // USPS: 23 digits, one over 22
    [InlineData("12345678901234567890123", true)]
    [InlineData("1234567890123456", false)] // FedEx: 16 digits, one over 15
    [InlineData("1234567890123456", true)]
    public void RunOneLongerThanEachPatternsMaximum_IsNotMatched(string number, bool allowSpaces)
    {
        TextFilterResult result = FilterThroughService(number, allowSpaces);

        Assert.Empty(result.Spans);
        Assert.Equal($"Ship {number} today", result.FilteredText);
    }

    [Theory]
    [InlineData("1Z999AA10123456784", false)]
    [InlineData("1Z999AA10123456784", true)]
    [InlineData("123456789012", false)]
    [InlineData("123456789012345", false)]
    [InlineData("12345678901234567890", false)]
    [InlineData("9400100000000000000000", false)]
    [InlineData("9400100000000000000000", true)]
    [InlineData("9400 1000 0000 0000 0000 00", true)]
    [InlineData("7489 1234 5678", true)]
    public void NumbersDetectedInFull_AreStillDetectedInFull(string number, bool allowSpaces)
    {
        TextFilterResult result = FilterThroughService(number, allowSpaces);

        Span span = Assert.Single(result.Spans);
        Assert.Equal(number, span.Text);
        Assert.Equal(5, span.CharacterStart);
    }

    [Theory]
    [InlineData("1Z999AA10123456784X")]
    [InlineData("1Z999AA10123456784ab")]
    public void UpsNumberFollowedDirectlyByLettersOrDigits_IsNotMatchedOnItsFirst18Characters(string number)
    {
        Assert.Empty(FilterThroughService(number).Spans);
    }

    [Theory]
    [InlineData("798429808620abc")]
    [InlineData("798429808620_1")]
    public void DigitsFollowedDirectlyByWordCharacters_AreNotMatched(string number)
    {
        Assert.Empty(FilterThroughService(number).Spans);
    }

    [Theory]
    [InlineData("Ship 798429808620.", "798429808620")]
    [InlineData("Ship (798429808620)", "798429808620")]
    [InlineData("Ship 1Z999AA10123456784, today", "1Z999AA10123456784")]
    public void NumberFollowedByPunctuation_IsStillDetected(string input, string number)
    {
        var policy = new PhileasPolicy
        {
            Name = "t",
            Identifiers = new Identifiers { TrackingNumber = new TrackingNumber() }
        };

        TextFilterResult result = new FilterService().Filter(policy, "ctx", 0, input);

        Assert.Equal(number, Assert.Single(result.Spans).Text);
    }
}
