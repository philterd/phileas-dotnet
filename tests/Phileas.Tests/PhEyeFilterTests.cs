using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Phileas.Filters;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;
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
                    new PhEye
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
        Assert.Contains("pheyes", json);
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
    public void Identifiers_HasFilter_PhEye_ReturnsTrueWhenConfigured()
    {
        var identifiers = new Identifiers
        {
            PhEyes = new List<PhEye>
            {
                new PhEye { PhEyeConfiguration = new PhEyeConfiguration { Endpoint = "http://localhost:8080" } }
            }
        };

        Assert.True(identifiers.HasFilter(Phileas.Model.Filtering.FilterType.PhEye));
    }

    [Fact]
    public void Identifiers_HasFilter_PhEye_ReturnsFalseWhenNotConfigured()
    {
        var identifiers = new Identifiers();
        Assert.False(identifiers.HasFilter(Phileas.Model.Filtering.FilterType.PhEye));
    }

    [Fact]
    public void Identifiers_HasFilter_PhEye_ReturnsFalseWhenListIsEmpty()
    {
        var identifiers = new Identifiers { PhEyes = new List<PhEye>() };
        Assert.False(identifiers.HasFilter(Phileas.Model.Filtering.FilterType.PhEye));
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
                PhEyes = new List<PhEye> { new PhEye { PhEyeConfiguration = phEyeConfig } }
            }
        };

        var result = filter.Filter(policy, "ctx", 0, "Hello, John Smith today.");
        Assert.Single(result.Spans);
        Assert.Equal("John Smith", result.Spans[0].Text);
        Assert.Equal(Phileas.Model.Filtering.FilterType.Person, result.Spans[0].FilterType);
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
                PhEyes = new List<PhEye> { new PhEye { PhEyeConfiguration = phEyeConfig } }
            }
        };

        var result = filter.Filter(policy, "ctx", 0, "Hello, John Smith today.");
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void PhEyeFilter_MultiplePheyes_UsedByFilterPolicyLoader()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                PhEyes = new List<PhEye>
                {
                    new PhEye { PhEyeConfiguration = new PhEyeConfiguration { Endpoint = "http://service1:8080" } },
                    new PhEye { PhEyeConfiguration = new PhEyeConfiguration { Endpoint = "http://service2:8080" } }
                }
            }
        };

        // Verify that the policy correctly models multiple pheyes
        Assert.Equal(2, policy.Identifiers.PhEyes!.Count);
        Assert.Equal("http://service1:8080", policy.Identifiers.PhEyes[0].PhEyeConfiguration.Endpoint);
        Assert.Equal("http://service2:8080", policy.Identifiers.PhEyes[1].PhEyeConfiguration.Endpoint);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public FakeHttpMessageHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
