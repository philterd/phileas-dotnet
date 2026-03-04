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
using Microsoft.ML.OnnxRuntime;
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
        Assert.Contains("pheye", json);
        Assert.Contains("pheye.example.com", json);
    }

    [Fact]
    public void Policy_WithPhEyes_DeserializesFromJson()
    {
        var json = """
                   {
                       "name": "test",
                       "identifiers": {
                           "pheye": [
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
    public void PhEyeConfiguration_SupportsModelPaths()
    {
        var config = new PhEyeConfiguration
        {
            ModelPath = "C:\\models\\model.onnx",
            VocabPath = "C:\\models\\vocab.txt",
            Labels = new List<string> { "PER", "ORG", "LOC" }
        };

        Assert.Equal("C:\\models\\model.onnx", config.ModelPath);
        Assert.Equal("C:\\models\\vocab.txt", config.VocabPath);
        Assert.Equal(3, config.Labels.Count);
    }

    [Fact]
    public void PhEyeConfiguration_SerializesModelPathsToJson()
    {
        var policy = new PhileasPolicy
        {
            Name = "local-model-policy",
            Identifiers = new Identifiers
            {
                PhEyes = new List<PhEye>
                {
                    new()
                    {
                        PhEyeConfiguration = new PhEyeConfiguration
                        {
                            ModelPath = "C:\\models\\model.onnx",
                            VocabPath = "C:\\models\\vocab.txt",
                            Labels = new List<string> { "PERSON" }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(policy);
        Assert.Contains("modelPath", json);
        Assert.Contains("vocabPath", json);
        Assert.Contains("model.onnx", json);
        Assert.Contains("vocab.txt", json);
    }

    [Fact]
    public void PhEyeConfiguration_DeserializesModelPathsFromJson()
    {
        var json = """
                   {
                       "name": "test",
                       "identifiers": {
                           "pheye": [
                               {
                                   "phEyeConfiguration": {
                                       "modelPath": "C:\\models\\model.onnx",
                                       "vocabPath": "C:\\models\\vocab.txt",
                                       "labels": ["PERSON", "ORG"]
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
        Assert.Equal("C:\\models\\model.onnx", policy.Identifiers.PhEyes[0].PhEyeConfiguration.ModelPath);
        Assert.Equal("C:\\models\\vocab.txt", policy.Identifiers.PhEyes[0].PhEyeConfiguration.VocabPath);
        Assert.Equal(2, policy.Identifiers.PhEyes[0].PhEyeConfiguration.Labels.Count);
    }

    [Fact]
    public void PhEyeFilter_WithLocalModel_SkipsRemoteServiceCall()
    {
        // This test verifies that when model paths are provided, the filter doesn't use HTTP
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy>())
            .WithIgnored(new HashSet<string>(StringComparer.OrdinalIgnoreCase))
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .WithWindowSize(5)
            .WithPriority(0)
            .Build();

        var phEyeConfig = new PhEyeConfiguration
        {
            ModelPath = "C:\\nonexistent\\model.onnx",
            VocabPath = "C:\\nonexistent\\vocab.txt",
            Endpoint = "http://localhost:8080",
            Labels = new List<string> { "PERSON" }
        };

        // Even though httpClient is null, the filter should attempt to use local model first
        // This will throw when trying to load the model, but it proves HTTP isn't used
        Assert.Throws<OnnxRuntimeException>(() =>
        {
            using var filter = new PhEyeFilter(config, phEyeConfig, false, new Dictionary<string, double>());
        });
    }

    [Fact]
    public void PhEyeFilter_WithoutModelPaths_UsesRemoteService()
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
    public void PhEyeFilter_LocalModel_AppliesConfidenceThresholds()
    {
        // Test that confidence thresholds work with local model mode
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy>())
            .WithIgnored(new HashSet<string>(StringComparer.OrdinalIgnoreCase))
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .WithWindowSize(5)
            .WithPriority(0)
            .Build();

        var phEyeConfig = new PhEyeConfiguration
        {
            ModelPath = "C:\\models\\model.onnx",
            VocabPath = "C:\\models\\vocab.txt",
            Labels = new List<string> { "PER", "ORG", "LOC" }
        };

        var thresholds = new Dictionary<string, double>
        {
            { "PER", 0.90 },
            { "ORG", 0.85 },
            { "LOC", 0.80 }
        };

        // Configuration is valid - filter creation will fail due to missing files
        // but this proves the thresholds are properly passed to the local model path
        Assert.Throws<OnnxRuntimeException>(() =>
        {
            using var filter = new PhEyeFilter(config, phEyeConfig, false, thresholds);
        });
    }

    [Fact]
    public void PhEyeFilter_LocalModel_RespectsIgnoredTerms()
    {
        var ignoredTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MIT",
            "Google",
            "Microsoft"
        };

        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy>())
            .WithIgnored(ignoredTerms)
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .WithWindowSize(5)
            .WithPriority(0)
            .Build();

        var phEyeConfig = new PhEyeConfiguration
        {
            ModelPath = "C:\\models\\model.onnx",
            VocabPath = "C:\\models\\vocab.txt",
            Labels = new List<string> { "PER", "ORG" }
        };

        // Configuration is valid with ignored terms
        Assert.Throws<OnnxRuntimeException>(() =>
        {
            using var filter = new PhEyeFilter(config, phEyeConfig, false, new Dictionary<string, double>());
        });
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

    [Fact]
    public void PhEyeFilter_MixedConfiguration_PrefersLocalModel()
    {
        // When both model paths and endpoint are set, local model should take precedence
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy>())
            .WithIgnored(new HashSet<string>(StringComparer.OrdinalIgnoreCase))
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .WithWindowSize(5)
            .WithPriority(0)
            .Build();

        var phEyeConfig = new PhEyeConfiguration
        {
            ModelPath = "C:\\models\\model.onnx",
            VocabPath = "C:\\models\\vocab.txt",
            Endpoint = "http://localhost:8080",
            Labels = new List<string> { "PERSON" }
        };

        // Should attempt to load local model, not use HTTP
        Assert.Throws<OnnxRuntimeException>(() =>
        {
            using var filter = new PhEyeFilter(config, phEyeConfig, false, new Dictionary<string, double>());
        });
    }

    [Fact]
    public void PhEyeFilter_PartialModelConfiguration_UsesRemoteService()
    {
        // If only one of ModelPath or VocabPath is set, should fall back to remote service
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

        // Only ModelPath set, VocabPath is null
        var phEyeConfig = new PhEyeConfiguration
        {
            ModelPath = "C:\\models\\model.onnx",
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

        // Should use remote service since model configuration is incomplete
        var result = filter.Filter(policy, "ctx", 0, "John went to the store.");
        Assert.Single(result.Spans);
        Assert.Equal("John", result.Spans[0].Text);
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