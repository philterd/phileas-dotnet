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

using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using System.Text.Json.Nodes;
using Xunit;
using PStrat = Phileas.Policy.Filters.Strategies;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
///     Serialization round-trip coverage for a full policy, mirroring the Java <c>PolicyTest</c>. The
///     non-canonical <c>ner</c> identifier from the original test is intentionally omitted (it is not in
///     the canonical schema and is harmlessly ignored on deserialization).
/// </summary>
public class PolicyModelTests
{
    [Fact]
    public void Serialize_FullPolicy_ProducesJson()
    {
        var policy = new PhileasPolicy
        {
            Ignored = new List<Ignored> { new() { Terms = { "ignored-term" } } },
            Identifiers = new Identifiers
            {
                CustomDictionaries = new List<CustomDictionary>
                {
                    new()
                    {
                        Terms = new List<string> { "123", "456", "jeff", "john" },
                        Strategies = new List<PStrat.CustomDictionaryFilterStrategy> { new() }
                    }
                },
                Age = new Age { Strategies = new List<PStrat.AgeFilterStrategy> { new() } },
                City = new City { Strategies = new List<PStrat.CityFilterStrategy> { new() } },
                County = new County { Strategies = new List<PStrat.CountyFilterStrategy> { new() } },
                FirstName = new FirstName { Strategies = new List<PStrat.FirstNameFilterStrategy> { new() } },
                Hospital = new Hospital { Strategies = new List<PStrat.HospitalFilterStrategy> { new() } },
                State = new State { Strategies = new List<PStrat.StateFilterStrategy> { new() } },
                Surname = new Surname { Strategies = new List<PStrat.SurnameFilterStrategy> { new() } },
                Ssn = new Ssn(),
                ZipCode = new ZipCode(),
                CustomIdentifiers = new List<Identifier> { new() },
                Sections = new List<Section> { new() { StartPattern = "BEGIN", EndPattern = "END" } }
            }
        };

        var json = PolicySerializer.SerializeToJson(policy);

        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.Contains("\"dictionaries\"", json);
        Assert.Contains("\"firstName\"", json);
    }

    [Fact]
    public void Deserialize_WithDictionariesAndIgnored()
    {
        const string json = """
                            {
                              "ignored": [ { "terms": [ "term1", "term2", "Jeff Smith" ] } ],
                              "identifiers": {
                                "dictionaries": [
                                  {
                                    "classification": "mylist",
                                    "terms": [ "123", "456", "jeff", "john" ],
                                    "sensitivity": "auto",
                                    "customFilterStrategies": [ { "strategy": "REDACT" } ]
                                  }
                                ],
                                "ner": { "nerFilterStrategies": [ { "strategy": "REDACT" } ] },
                                "age": { "ageFilterStrategies": [ { "strategy": "REDACT" } ] },
                                "ssn": { "ssnFilterStrategies": [ { "strategy": "REDACT" } ] },
                                "zipCode": { "zipCodeFilterStrategy": [ { "strategy": "REDACT" } ] }
                              }
                            }
                            """;

        var policy = PolicySerializer.DeserializeFromJson(json);

        Assert.NotNull(policy.Identifiers.CustomDictionaries);
        Assert.NotEmpty(policy.Identifiers.CustomDictionaries!);
        Assert.NotEmpty(policy.Ignored);
        Assert.True(policy.Identifiers.HasFilter(FilterType.CustomDictionary));
        Assert.NotNull(policy.Identifiers.Age);
        Assert.NotNull(policy.Identifiers.ZipCode);
    }

    [Fact]
    public void Deserialize_WithoutDictionaries()
    {
        const string json = """
                            {
                              "identifiers": {
                                "age": { "ageFilterStrategies": [ { "strategy": "REDACT" } ] },
                                "ssn": { "ssnFilterStrategies": [ { "strategy": "REDACT" } ] }
                              }
                            }
                            """;

        var policy = PolicySerializer.DeserializeFromJson(json);

        Assert.True(policy.Identifiers.CustomDictionaries is null || policy.Identifiers.CustomDictionaries.Count == 0);
        Assert.Empty(policy.Ignored);
        Assert.False(policy.Identifiers.HasFilter(FilterType.CustomDictionary));
    }

    [Theory]
    [InlineData("zipCodeFilterStrategies")]
    [InlineData("zipCodeFilterStrategy")]
    public void ZipCode_AcceptsBothStrategyKeys(string key)
    {
        // The singular was the only accepted name before schema 1.3.0, so a policy written
        // against an earlier schema must still deserialize its strategies.
        var json = $$"""
                     { "identifiers": { "zipCode": { "{{key}}": [ { "strategy": "REDACT" } ] } } }
                     """;

        var policy = PolicySerializer.DeserializeFromJson(json);

        Assert.Equal(1, policy.Identifiers.ZipCode?.Strategies?.Count);

        // The plural is the canonical name, so it is the only one written back out.
        var serialized = PolicySerializer.SerializeToJson(policy);
        Assert.Contains("zipCodeFilterStrategies", serialized);
        Assert.DoesNotContain("\"zipCodeFilterStrategy\"", serialized);
    }

    [Fact]
    public void Metadata_RoundTrips()
    {
        // Keys beyond description are allowed by the schema, so they have to survive too.
        const string json = """
                            {
                              "metadata": {
                                "description": "Client intake forms.",
                                "author": "records team",
                                "labels": [ "intake", "pii" ]
                              },
                              "identifiers": {
                                "ssn": { "ssnFilterStrategies": [ { "strategy": "REDACT" } ] }
                              }
                            }
                            """;

        var policy = PolicySerializer.DeserializeFromJson(json);

        Assert.NotNull(policy.Metadata);
        Assert.Equal("Client intake forms.", (string?)policy.Metadata!["description"]);
        Assert.Equal("records team", (string?)policy.Metadata["author"]);

        // Re-serializing must not drop or alter any of it.
        var before = JsonNode.Parse(json)!["metadata"]!.ToJsonString();
        var after = JsonNode.Parse(PolicySerializer.SerializeToJson(policy))!["metadata"]!.ToJsonString();
        Assert.Equal(before, after);
    }

    [Fact]
    public void Metadata_OmittedWhenAbsent()
    {
        const string json = """
                            { "identifiers": { "ssn": { "ssnFilterStrategies": [ { "strategy": "REDACT" } ] } } }
                            """;

        var policy = PolicySerializer.DeserializeFromJson(json);

        Assert.Null(policy.Metadata);
        Assert.DoesNotContain("metadata", PolicySerializer.SerializeToJson(policy));
    }
}
