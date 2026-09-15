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
using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;
using PolicySchema = Phileas.Policy.PolicySchema;
using Serializer = Phileas.Policy.PolicySerializer;

namespace Phileas.Tests;

public class PolicyTests
{
    [Fact]
    public void Policy_SerializesToJson()
    {
        var policy = new PhileasPolicy
        {
            // Name is an in-memory convenience label only; the canonical Phileas policy JSON has no
            // top-level "name", so it is intentionally not serialized.
            Name = "test-policy",
            Identifiers = new Identifiers
            {
                EmailAddress = new EmailAddress
                {
                    Strategies = new List<EmailAddressFilterStrategy>
                    {
                        new() { Strategy = "REDACT" }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(policy);
        Assert.DoesNotContain("test-policy", json);
        Assert.Contains("emailAddress", json);
    }

    [Fact]
    public void Policy_DeserializesFromJson()
    {
        var json = """
                   {
                       "identifiers": {
                           "emailAddress": {
                               "emailAddressFilterStrategies": [{"strategy": "REDACT"}]
                           }
                       }
                   }
                   """;

        var policy = JsonSerializer.Deserialize<PhileasPolicy>(json);
        Assert.NotNull(policy);
        Assert.NotNull(policy.Identifiers.EmailAddress);
    }

    // The phone number filter's region property (policy schema 1.2.0) accepts either a single string or an
    // array of them; both shapes normalize to a list, matching the Java StringOrArrayListDeserializer.
    [Fact]
    public void PhoneNumber_DeserializesRegionString()
    {
        var json = """
                   {
                       "identifiers": {
                           "phoneNumber": {
                               "region": "GB",
                               "phoneNumberFilterStrategies": []
                           }
                       }
                   }
                   """;

        var policy = Serializer.DeserializeFromJson(json);

        Assert.Equal(new List<string> { "GB" }, policy.Identifiers.PhoneNumber!.GetRegionOrDefault());
    }

    [Fact]
    public void PhoneNumber_DeserializesRegionArray()
    {
        var json = """
                   {
                       "identifiers": {
                           "phoneNumber": {
                               "region": ["US", "GB", "FR"],
                               "phoneNumberFilterStrategies": []
                           }
                       }
                   }
                   """;

        var policy = Serializer.DeserializeFromJson(json);

        Assert.Equal(new List<string> { "US", "GB", "FR" }, policy.Identifiers.PhoneNumber!.GetRegionOrDefault());
    }

    [Fact]
    public void PhoneNumber_DefaultsRegionToUs()
    {
        var json = """
                   {
                       "identifiers": {
                           "phoneNumber": {
                               "phoneNumberFilterStrategies": []
                           }
                       }
                   }
                   """;

        var policy = Serializer.DeserializeFromJson(json);

        Assert.Null(policy.Identifiers.PhoneNumber!.Region);
        Assert.Equal(new List<string> { "US" }, policy.Identifiers.PhoneNumber.GetRegionOrDefault());
    }

    [Fact]
    public void PhoneNumber_SerializesRegionAsAnArray()
    {
        // Both input shapes are written back out as an array, as the Java implementation does. A policy with
        // no region set emits none, so a round trip does not bake the default into the document.
        var policy = new PhileasPolicy
        {
            Identifiers = new Identifiers { PhoneNumber = new PhoneNumber { Region = new List<string> { "GB" } } }
        };

        var json = Serializer.SerializeToJson(policy);

        Assert.Contains("\"region\":[\"GB\"]", json);
        Assert.True(PolicySchema.Validate(json));

        policy.Identifiers.PhoneNumber!.Region = null;
        Assert.DoesNotContain("region", Serializer.SerializeToJson(policy));
    }

    [Fact]
    public void Identifiers_HasFilter_ReturnsCorrectly()
    {
        var identifiers = new Identifiers
        {
            EmailAddress = new EmailAddress()
        };

        Assert.True(identifiers.HasFilter(FilterType.EmailAddress));
        Assert.False(identifiers.HasFilter(FilterType.Ssn));
    }
}