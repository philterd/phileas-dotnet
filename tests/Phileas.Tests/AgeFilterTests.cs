using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;
using Phileas.Policy.Filters;
using Phileas.Filters.Regex;
using Phileas.Strategies.Rules;
using Xunit;

namespace Phileas.Tests;

public class AgeFilterTests
{
    private static AgeFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new AgeFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new AgeFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Age = new Age() }
        };
    }

    [Theory]
    [InlineData("The patient is 45 years old.")]
    [InlineData("He is 30 yo.")]
    [InlineData("age 25")]
    [InlineData("aged 65")]
    [InlineData("She is 22 y/o.")]
    public void Filter_DetectsAge(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
    }

    [Fact]
    public void Filter_ReplacesAgeWithRedaction()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, "The patient is 45 years old.");
        Assert.All(result.Spans, span => Assert.Contains("REDACTED", span.Replacement));
    }
}
