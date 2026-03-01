using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;
using Phileas.Policy.Filters;
using Phileas.Services.Filters.Regex;
using Phileas.Services.Strategies.Rules;
using Xunit;

namespace Phileas.Tests;

public class ZipCodeFilterTests
{
    private static ZipCodeFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new ZipCodeFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new ZipCodeFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { ZipCode = new ZipCode() }
        };
    }

    [Theory]
    [InlineData("ZIP: 12345")]
    [InlineData("ZIP+4: 12345-6789")]
    public void Filter_DetectsZipCode(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
    }
}
