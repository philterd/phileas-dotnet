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
using Phileas;
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

        var result = FilterPolicyLoader.Filter(policy, "test", 0, "Contact john.doe@example.com for help.");
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

        var result = FilterPolicyLoader.Filter(policy, "test", 0, "SSN: 123-45-6789");
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
        var result = FilterPolicyLoader.Filter(policy, "test", 0, input);
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

        var result = FilterPolicyLoader.Filter(policy, "test", 0, "Email: user@example.com");
        Assert.NotEmpty(result.Spans);
        Assert.Equal(Phileas.Model.Filtering.FilterType.EmailAddress, result.Spans[0].FilterType);
        Assert.Equal("user@example.com", result.Spans[0].Text);
    }
}
