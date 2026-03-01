using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;
using Phileas.Policy.Filters;
using Phileas.Services.Filters.Regex;
using Phileas.Services.Strategies.Rules;
using Xunit;

namespace Phileas.Tests;

public class SsnFilterTests
{
    private static SsnFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new SsnFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new SsnFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Ssn = new Ssn() }
        };
    }

    [Theory]
    [InlineData("SSN: 123-45-6789")]
    [InlineData("My SSN is 123 45 6789")]
    [InlineData("Social security: 123456789")]
    public void Filter_DetectsSsn(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
    }

    [Fact]
    public void Filter_ReturnsCorrectFilterType()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, "SSN: 123-45-6789");
        Assert.Equal(FilterType.Ssn, result.Spans[0].FilterType);
    }
}
