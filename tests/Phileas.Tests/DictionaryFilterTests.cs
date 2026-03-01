using Phileas.Filters;
using Phileas.Filters.Regex;
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Strategies.Rules;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

public class DictionaryFilterTests
{
    private static DictionaryFilter CreateFilter(IEnumerable<string> terms)
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new DictionaryFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new DictionaryFilter(config, terms);
    }

    private static PhileasPolicy CreatePolicy(IEnumerable<string> terms)
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                Dictionaries = new List<Phileas.Policy.Filters.Dictionary>
                {
                    new Phileas.Policy.Filters.Dictionary { Terms = terms.ToList() }
                }
            }
        };
    }

    [Theory]
    [InlineData("The patient has diabetes.", "diabetes")]
    [InlineData("Diagnosis: hypertension.", "hypertension")]
    [InlineData("She was treated for asthma.", "asthma")]
    public void Filter_DetectsTermInText(string input, string term)
    {
        var filter = CreateFilter(new[] { term });
        var policy = CreatePolicy(new[] { term });
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.Dictionary, result.Spans[0].FilterType);
        Assert.Equal(term, result.Spans[0].Text, ignoreCase: true);
    }

    [Theory]
    [InlineData("No sensitive terms here.", new[] { "diabetes", "hypertension" })]
    [InlineData("The patient is healthy.", new[] { "cancer" })]
    public void Filter_DoesNotDetectAbsentTerms(string input, string[] terms)
    {
        var filter = CreateFilter(terms);
        var policy = CreatePolicy(terms);
        var result = filter.Filter(policy, "test", 0, input);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void Filter_IsCaseInsensitive()
    {
        var terms = new[] { "Diabetes" };
        var filter = CreateFilter(terms);
        var policy = CreatePolicy(terms);
        var result = filter.Filter(policy, "test", 0, "The patient has DIABETES.");
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.Dictionary, result.Spans[0].FilterType);
    }

    [Fact]
    public void Filter_DetectsMultipleTerms()
    {
        var terms = new[] { "diabetes", "hypertension" };
        var filter = CreateFilter(terms);
        var policy = CreatePolicy(terms);
        var result = filter.Filter(policy, "test", 0, "Patient has diabetes and hypertension.");
        Assert.Equal(2, result.Spans.Count);
    }

    [Fact]
    public void FilterPolicyLoader_RedactsDictionaryTerms()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                Dictionaries = new List<Phileas.Policy.Filters.Dictionary>
                {
                    new Phileas.Policy.Filters.Dictionary
                    {
                        Name = "conditions",
                        Terms = new List<string> { "diabetes", "hypertension" }
                    }
                }
            }
        };

        var result = FilterPolicyLoader.Filter(policy, "test", 0, "The patient has diabetes and hypertension.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("diabetes", result.FilteredText);
        Assert.DoesNotContain("hypertension", result.FilteredText);
    }

    [Fact]
    public void FilterPolicyLoader_SupportsMultipleDictionaries()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                Dictionaries = new List<Phileas.Policy.Filters.Dictionary>
                {
                    new Phileas.Policy.Filters.Dictionary
                    {
                        Name = "conditions",
                        Terms = new List<string> { "diabetes" }
                    },
                    new Phileas.Policy.Filters.Dictionary
                    {
                        Name = "medications",
                        Terms = new List<string> { "metformin" }
                    }
                }
            }
        };

        var result = FilterPolicyLoader.Filter(policy, "test", 0, "Patient with diabetes takes metformin daily.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("diabetes", result.FilteredText);
        Assert.DoesNotContain("metformin", result.FilteredText);
    }
}
