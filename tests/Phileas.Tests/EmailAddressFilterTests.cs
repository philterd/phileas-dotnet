using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;
using Phileas.Policy.Filters;
using Phileas.Services.Filters.Regex;
using Phileas.Services.Strategies.Rules;
using Xunit;

namespace Phileas.Tests;

public class EmailAddressFilterTests
{
    private static EmailAddressFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new EmailAddressFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new EmailAddressFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { EmailAddress = new EmailAddress() }
        };
    }

    [Theory]
    [InlineData("Contact us at john.doe@example.com for help.")]
    [InlineData("Send to user+tag@domain.org")]
    [InlineData("Email: test.user@sub.domain.co.uk")]
    public void Filter_DetectsEmail(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.EmailAddress, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("No email here")]
    [InlineData("not-an-email")]
    public void Filter_DoesNotDetectNonEmail(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.Empty(result.Spans);
    }
}
