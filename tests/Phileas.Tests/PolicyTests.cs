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
