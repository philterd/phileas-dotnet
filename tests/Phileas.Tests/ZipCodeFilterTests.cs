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
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

public class ZipCodeFilterTests
{
    private static ZipCodeFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new ZipCodeFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new ZipCodeFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { ZipCode = new ZipCode() }
        };
    }

    [Theory]
    [InlineData("ZIP: 12345")]
    [InlineData("ZIP+4: 12345-6789")]
    public void Filter_DetectsZipCode(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
    }

    [Theory]
    [InlineData("No zip here.")]
    [InlineData("Short: 1234")] // Only 4 digits
    public void Filter_DoesNotDetectNonZipCode(string input)
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
        var result = filter.Filter(policy, "test", 0, "ZIP: 90210");
        Assert.Equal(FilterType.ZipCode, result.Spans[0].FilterType);
    }

    [Fact]
    public void Filter_DetectsZipPlusFourCorrectly()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, "Zip+4: 90210-1234");
        Assert.Single(result.Spans);
        Assert.Equal("90210-1234", result.Spans[0].Text);
    }

    // Five digits joined by a hyphen to a letter or digit before them are part of a longer identifier.
    // See #193 (philterd/phileas#485).
    private static ZipCodeFilter CreateFilter(bool requireDelimiter, bool validate = false)
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new ZipCodeFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new ZipCodeFilter(config, requireDelimiter, validate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Filter_DoesNotDetectDigitsJoinedByAHyphen(bool requireDelimiter)
    {
        const string input = "Re: Matter No. 2026-00847\nSend it to Boston, MA 02116.\n"
                             + "Reference FR-48213, invoice INV-12345, case 1:26-cv-00847.";
        var result = CreateFilter(requireDelimiter).Filter(CreatePolicy(), "test", 0, input);

        var span = Assert.Single(result.Spans);
        Assert.Equal("02116", span.Text);
    }

    [Theory]
    [InlineData("­")]
    [InlineData("‐")]
    [InlineData("‑")]
    [InlineData("‒")]
    [InlineData("−")]
    [InlineData("﹣")]
    [InlineData("－")]
    public void Filter_DoesNotDetectDigitsJoinedByAHyphenSubstitute(string hyphen)
    {
        foreach (var requireDelimiter in new[] { true, false })
        {
            var result = CreateFilter(requireDelimiter)
                .Filter(CreatePolicy(), "test", 0, $"Reference FR{hyphen}48213 was filed.");
            Assert.Empty(result.Spans);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Filter_DetectsBothZipCodesOfARange(bool requireDelimiter)
    {
        var result = CreateFilter(requireDelimiter)
            .Filter(CreatePolicy(), "test", 0, "Serving ZIP codes 02116-02118 only.");

        Assert.Equal(new[] { "02116", "02118" }, result.Spans.Select(s => s.Text));
    }

    [Fact]
    public void FilterAndValidate_DoesNotDetectARealZipCodeJoinedByAHyphen()
    {
        // 48213 is a real ZIP code (Detroit), so validation alone does not drop it.
        var result = CreateFilter(true, true)
            .Filter(CreatePolicy(), "test", 0, "Reference FR-48213 was filed in Boston, MA 02116.");

        var span = Assert.Single(result.Spans);
        Assert.Equal("02116", span.Text);
        Assert.True(span.Applied);
    }

    [Theory]
    [InlineData("Send it to Boston, MA 02116.", "02116")]
    [InlineData("Send it to Boston, MA 02116-1234.", "02116-1234")]
    [InlineData("Send it to Boston 02116 today.", "02116")]
    [InlineData("zip:02116", "02116")]
    [InlineData("(02116)", "02116")]
    [InlineData("Boston, MA, 02116-1234", "02116-1234")]
    [InlineData("Boston, MA – 02116", "02116")]
    public void Filter_DetectsZipCodesInOrdinaryUse(string input, string zipCode)
    {
        foreach (var requireDelimiter in new[] { true, false })
        {
            var result = CreateFilter(requireDelimiter).Filter(CreatePolicy(), "test", 0, input);

            var span = Assert.Single(result.Spans);
            Assert.Equal(zipCode, span.Text);
        }
    }
}