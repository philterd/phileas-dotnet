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

using System.Text.Json;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

public class PolicyTests
{
    [Fact]
    public void Policy_SerializesToJson()
    {
        var policy = new PhileasPolicy
        {
            Name = "test-policy",
            Identifiers = new Identifiers
            {
                EmailAddress = new EmailAddress
                {
                    Strategies = new List<EmailAddressFilterStrategy>
                    {
                        new EmailAddressFilterStrategy { Strategy = "REDACT" }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(policy);
        Assert.Contains("test-policy", json);
        Assert.Contains("emailAddress", json);
    }

    [Fact]
    public void Policy_DeserializesFromJson()
    {
        var json = """
        {
            "name": "test",
            "identifiers": {
                "emailAddress": {
                    "emailAddressFilterStrategies": [{"strategy": "REDACT"}]
                }
            }
        }
        """;

        var policy = JsonSerializer.Deserialize<PhileasPolicy>(json);
        Assert.NotNull(policy);
        Assert.Equal("test", policy.Name);
        Assert.NotNull(policy.Identifiers.EmailAddress);
    }

    [Fact]
    public void Identifiers_HasFilter_ReturnsCorrectly()
    {
        var identifiers = new Identifiers
        {
            EmailAddress = new EmailAddress()
        };

        Assert.True(identifiers.HasFilter(Phileas.Model.Filtering.FilterType.EmailAddress));
        Assert.False(identifiers.HasFilter(Phileas.Model.Filtering.FilterType.Ssn));
    }
}
