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

using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

public class FilterServiceTests
{
    [Fact]
    public void Filter_RedactsEmailFromText()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                EmailAddress = new EmailAddress()
            }
        };

        var result = new FilterService().Filter(policy, "test", 0, "Contact john.doe@example.com for help.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("john.doe@example.com", result.FilteredText);
    }

    [Fact]
    public void Filter_RedactsSsnFromText()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                Ssn = new Ssn()
            }
        };

        var result = new FilterService().Filter(policy, "test", 0, "SSN: 123-45-6789");
        Assert.Contains("REDACTED", result.FilteredText);
    }

    [Fact]
    public void Filter_ReturnsUnchangedTextWhenNoFiltersMatch()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                EmailAddress = new EmailAddress()
            }
        };

        const string input = "No PII here.";
        var result = new FilterService().Filter(policy, "test", 0, input);
        Assert.Equal(input, result.FilteredText);
    }

    [Fact]
    public void Filter_ReturnsSpansWithCorrectInfo()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                EmailAddress = new EmailAddress()
            }
        };

        var result = new FilterService().Filter(policy, "test", 0, "Email: user@example.com");
        Assert.NotEmpty(result.Spans);
        Assert.Equal(Phileas.Model.FilterType.EmailAddress, result.Spans[0].FilterType);
        Assert.Equal("user@example.com", result.Spans[0].Text);
    }

    [Fact]
    public void Filter_DefaultPostFilters_AllEnabled()
    {
        var policy = new PhileasPolicy { Name = "test" };
        Assert.True(policy.PostFilters.TrailingNewLines);
        Assert.True(policy.PostFilters.TrailingPeriods);
        Assert.True(policy.PostFilters.TrailingSpaces);
    }

    [Fact]
    public void Filter_PostFilters_CanBeDisabledInPolicy()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Ssn = new Ssn() },
            PostFilters = new PostFilters { TrailingNewLines = false, TrailingPeriods = false, TrailingSpaces = false }
        };

        // SSN regex uses \b so trailing chars are not captured; ensure filter still works normally
        var result = new FilterService().Filter(policy, "test", 0, "SSN: 123-45-6789");
        Assert.Single(result.Spans);
        Assert.Equal("123-45-6789", result.Spans[0].Text);
    }

    [Fact]
    public void Policy_PostFilters_SerializesToJson()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            PostFilters = new PostFilters { TrailingNewLines = false, TrailingPeriods = true, TrailingSpaces = false }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(policy);
        Assert.Contains("postFilters", json);
        Assert.Contains("trailingNewLines", json);
        Assert.Contains("trailingPeriods", json);
        Assert.Contains("trailingSpaces", json);
    }

    [Fact]
    public void Policy_PostFilters_DeserializesFromJson()
    {
        var json = """
        {
            "name": "test",
            "postFilters": {
                "trailingNewLines": false,
                "trailingPeriods": true,
                "trailingSpaces": false
            }
        }
        """;

        var policy = System.Text.Json.JsonSerializer.Deserialize<PhileasPolicy>(json);
        Assert.NotNull(policy);
        Assert.False(policy.PostFilters.TrailingNewLines);
        Assert.True(policy.PostFilters.TrailingPeriods);
        Assert.False(policy.PostFilters.TrailingSpaces);
    }
}
