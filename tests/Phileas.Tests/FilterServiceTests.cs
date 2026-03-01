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
