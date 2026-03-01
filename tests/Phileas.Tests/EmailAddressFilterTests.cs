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

public class EmailAddressFilterTests
{
    private static EmailAddressFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new EmailAddressFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new EmailAddressFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { EmailAddress = new EmailAddress() }
        };
    }

    [Theory]
    [InlineData("Contact us at john.doe@example.com for help.")]
    [InlineData("Send to user+tag@domain.org")]
    [InlineData("Email: test.user@sub.domain.co.uk")]
    public void Filter_DetectsEmail(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.EmailAddress, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("No email here")]
    [InlineData("not-an-email")]
    public void Filter_DoesNotDetectNonEmail(string input)
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
    public void Filter_DetectsMultipleEmailsInText()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, "Contact a@a.com or b@b.com for help.");
        Assert.Equal(2, result.Spans.Count);
        Assert.All(result.Spans, span => Assert.Equal(FilterType.EmailAddress, span.FilterType));
    }

    [Fact]
    public void Filter_ReturnsCorrectSpanPositions()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        const string input = "Email: user@example.com end";
        var result = filter.Filter(policy, "test", 0, input);
        Assert.Single(result.Spans);
        Assert.Equal("user@example.com", result.Spans[0].Text);
        Assert.Equal(7, result.Spans[0].CharacterStart);
        Assert.Equal(23, result.Spans[0].CharacterEnd);
    }
}
