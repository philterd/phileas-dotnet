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

using System.Net;
using System.Text;
using System.Text.Json;
using Phileas.Filters;
using Phileas.Filters.PhEye;
using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;
using AbstractFilterStrategy = Phileas.Filters.AbstractFilterStrategy;

namespace Phileas.Tests;

public class PhEyeFilterTests
{
    [Fact]
    public void Policy_WithPhEyes_SerializesToJson()
    {
        var policy = new PhileasPolicy
        {
            Name = "pheye-policy",
            Identifiers = new Identifiers
            {
                PhEyes = new List<PhEye>
                {
                    new()
                    {
                        PhEyeConfiguration = new PhEyeConfiguration
                        {
                            Endpoint = "http://pheye.example.com",
                            Labels = new List<string> { "PERSON" }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(policy);
        Assert.Contains("\"pheyes\"", json);
        Assert.Contains("pheye.example.com", json);
    }

    [Fact]
    public void Policy_WithPhEyes_DeserializesFromJson()
    {
        var json = """
                   {
                       "name": "test",
                       "identifiers": {
                           "pheyes": [
                               {
                                   "phEyeConfiguration": {
                                       "endpoint": "http://localhost:8080",
                                       "labels": ["PERSON"]
                                   },
                                   "removePunctuation": false
                               }
                           ]
                       }
                   }
                   """;

        var policy = JsonSerializer.Deserialize<PhileasPolicy>(json);
        Assert.NotNull(policy);
        Assert.NotNull(policy.Identifiers.PhEyes);
        Assert.Single(policy.Identifiers.PhEyes);
        Assert.Equal("http://localhost:8080", policy.Identifiers.PhEyes[0].PhEyeConfiguration.Endpoint);
    }

    [Fact]
    public void Policy_FromPhiSqlCompiledLocalModel_BindsPhEyeAndModelPath()
    {
        // A vendored copy of PhiSQL 1.1.0's compiled `pheye-local-model` example: a DETECT PHEYE ... MODEL '<path>'
        // policy, which compiles to the canonical `identifiers.pheyes` block with `modelPath`. This locks in that a
        // PhiSQL-produced local-model policy actually reaches the on-device GLiNER (ONNX) path, rather than the PhEye
        // block being silently dropped because of a key mismatch.
        var json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Resources", "Policies", "pheye-local-model.json"));

        var policy = PolicySerializer.DeserializeFromJson(json);

        Assert.NotNull(policy.Identifiers.PhEyes);
        var phEye = Assert.Single(policy.Identifiers.PhEyes);
        // modelPath set => PhEyeFilter runs local in-process inference instead of calling a remote endpoint.
        Assert.Equal("/models/ph-eye-pii-base", phEye.PhEyeConfiguration.ModelPath);
        Assert.Equal(new[] { "person", "email address", "phone number" }, phEye.PhEyeConfiguration.Labels);
    }

    [Fact]
    public void Identifiers_HasFilter_PhEye_ReturnsTrueWhenConfigured()
    {
        var identifiers = new Identifiers
        {
            PhEyes = new List<PhEye>
            {
                new() { PhEyeConfiguration = new PhEyeConfiguration { Endpoint = "http://localhost:8080" } }
            }
        };

        Assert.True(identifiers.HasFilter(FilterType.PhEye));
    }

    [Fact]
    public void Identifiers_HasFilter_PhEye_ReturnsFalseWhenNotConfigured()
    {
        var identifiers = new Identifiers();
        Assert.False(identifiers.HasFilter(FilterType.PhEye));
    }

    [Fact]
    public void Identifiers_HasFilter_PhEye_ReturnsFalseWhenListIsEmpty()
    {
        var identifiers = new Identifiers { PhEyes = new List<PhEye>() };
        Assert.False(identifiers.HasFilter(FilterType.PhEye));
    }

    [Fact]
    public void PhEyeFilter_ReturnsSpansFromRemoteService()
    {
        var phEyeResponseJson = """
                                [
                                    {"start": 7, "end": 17, "label": "PERSON", "text": "John Smith", "score": 0.99}
                                ]
                                """;

        var fakeHandler = new FakeHttpMessageHandler(phEyeResponseJson);
        var httpClient = new HttpClient(fakeHandler);

        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy>())
            .WithIgnored(new HashSet<string>(StringComparer.OrdinalIgnoreCase))
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .WithWindowSize(5)
            .WithPriority(0)
            .Build();

        var phEyeConfig = new PhEyeConfiguration
        {
            Endpoint = "http://localhost:8080",
            Labels = new List<string> { "PERSON" }
        };

        var filter = new PhEyeFilter(config, phEyeConfig, false, new Dictionary<string, double>(), httpClient);

        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                PhEyes = new List<PhEye> { new() { PhEyeConfiguration = phEyeConfig } }
            }
        };

        var result = filter.Filter(policy, "ctx", 0, "Hello, John Smith today.");
        Assert.Single(result.Spans);
        Assert.Equal("John Smith", result.Spans[0].Text);
        Assert.Equal(FilterType.Person, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("John Smith", "JS")]
    [InlineData("john", "J")]
    public void PhEyeFilter_Abbreviate_ReturnsInitials(string token, string expected)
    {
        var phEyeResponseJson = JsonSerializer.Serialize(new[]
        {
            new { start = 0, end = token.Length, label = "PERSON", text = token, score = 0.99 }
        });

        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy>
            {
                new Phileas.Filters.Strategies.Rules.PhEyeFilterStrategy
                {
                    Strategy = AbstractFilterStrategy.Abbreviate
                }
            })
            .WithIgnored(new HashSet<string>(StringComparer.OrdinalIgnoreCase))
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .WithWindowSize(5)
            .WithPriority(0)
            .Build();

        var phEyeConfig = new PhEyeConfiguration
        {
            Endpoint = "http://localhost:8080",
            Labels = new List<string> { "PERSON" }
        };

        using var filter = new PhEyeFilter(config, phEyeConfig, false, new Dictionary<string, double>(),
            new HttpClient(new FakeHttpMessageHandler(phEyeResponseJson)));

        var result = filter.Filter(new PhileasPolicy(), "ctx", 0, token);

        var span = Assert.Single(result.Spans);
        Assert.Equal(FilterType.Person, span.FilterType);
        Assert.Equal(expected, span.Replacement);
    }

    [Fact]
    public void PhEyeFilter_FiltersOutSpansBelowThreshold()
    {
        var phEyeResponseJson = """
                                [
                                    {"start": 7, "end": 17, "label": "PERSON", "text": "John Smith", "score": 0.50}
                                ]
                                """;

        var fakeHandler = new FakeHttpMessageHandler(phEyeResponseJson);
        var httpClient = new HttpClient(fakeHandler);

        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy>())
            .WithIgnored(new HashSet<string>(StringComparer.OrdinalIgnoreCase))
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .WithWindowSize(5)
            .WithPriority(0)
            .Build();

        var phEyeConfig = new PhEyeConfiguration
        {
            Endpoint = "http://localhost:8080",
            Labels = new List<string> { "PERSON" }
        };

        var thresholds = new Dictionary<string, double> { { "PERSON", 0.80 } };
        var filter = new PhEyeFilter(config, phEyeConfig, false, thresholds, httpClient);

        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                PhEyes = new List<PhEye> { new() { PhEyeConfiguration = phEyeConfig } }
            }
        };

        var result = filter.Filter(policy, "ctx", 0, "Hello, John Smith today.");
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void PhEyeFilter_MultiplePheyes_UsedByFilterService()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                PhEyes = new List<PhEye>
                {
                    new() { PhEyeConfiguration = new PhEyeConfiguration { Endpoint = "http://service1:8080" } },
                    new() { PhEyeConfiguration = new PhEyeConfiguration { Endpoint = "http://service2:8080" } }
                }
            }
        };

        // Verify that the policy correctly models multiple pheye
        Assert.Equal(2, policy.Identifiers.PhEyes!.Count);
        Assert.Equal("http://service1:8080", policy.Identifiers.PhEyes[0].PhEyeConfiguration.Endpoint);
        Assert.Equal("http://service2:8080", policy.Identifiers.PhEyes[1].PhEyeConfiguration.Endpoint);
    }

    [Fact]
    public void PhEyeFilter_UsesRemoteService()
    {
        var phEyeResponseJson = """
                                [
                                    {"start": 0, "end": 4, "label": "PERSON", "text": "John", "score": 0.95}
                                ]
                                """;

        var fakeHandler = new FakeHttpMessageHandler(phEyeResponseJson);
        var httpClient = new HttpClient(fakeHandler);

        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy>())
            .WithIgnored(new HashSet<string>(StringComparer.OrdinalIgnoreCase))
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .WithWindowSize(5)
            .WithPriority(0)
            .Build();

        var phEyeConfig = new PhEyeConfiguration
        {
            Endpoint = "http://localhost:8080",
            Labels = new List<string> { "PERSON" }
        };

        using var filter = new PhEyeFilter(config, phEyeConfig, false, new Dictionary<string, double>(), httpClient);

        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                PhEyes = new List<PhEye> { new() { PhEyeConfiguration = phEyeConfig } }
            }
        };

        var result = filter.Filter(policy, "ctx", 0, "John went to the store.");
        Assert.Single(result.Spans);
        Assert.Equal("John", result.Spans[0].Text);
    }

    [Fact]
    public void PhEyeFilter_DisposesResourcesProperly()
    {
        var phEyeResponseJson = """
                                [
                                    {"start": 0, "end": 4, "label": "PERSON", "text": "John", "score": 0.95}
                                ]
                                """;

        var fakeHandler = new FakeHttpMessageHandler(phEyeResponseJson);
        var httpClient = new HttpClient(fakeHandler);

        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy>())
            .WithIgnored(new HashSet<string>(StringComparer.OrdinalIgnoreCase))
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .WithWindowSize(5)
            .WithPriority(0)
            .Build();

        var phEyeConfig = new PhEyeConfiguration
        {
            Endpoint = "http://localhost:8080",
            Labels = new List<string> { "PERSON" }
        };

        var filter = new PhEyeFilter(config, phEyeConfig, false, new Dictionary<string, double>(), httpClient);

        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                PhEyes = new List<PhEye> { new() { PhEyeConfiguration = phEyeConfig } }
            }
        };

        var result = filter.Filter(policy, "ctx", 0, "John went to the store.");
        Assert.Single(result.Spans);

        // Dispose should not throw
        filter.Dispose();

        // Calling Dispose multiple times should be safe
        filter.Dispose();
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public FakeHttpMessageHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
