using Phileas.Filters;
using Phileas.Policy.Filters.Regex;
using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Filters.Strategies.Rules;
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

    private static DictionaryFilter CreateFuzzyFilter(IEnumerable<string> terms, string level = "low")
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new DictionaryFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new DictionaryFilter(config, terms, fuzzy: true, level: level);
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
    public void FilterService_RedactsDictionaryTerms()
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

        var result = FilterService.Filter(policy, "test", 0, "The patient has diabetes and hypertension.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("diabetes", result.FilteredText);
        Assert.DoesNotContain("hypertension", result.FilteredText);
    }

    [Fact]
    public void FilterService_SupportsMultipleDictionaries()
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

        var result = FilterService.Filter(policy, "test", 0, "Patient with diabetes takes metformin daily.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("diabetes", result.FilteredText);
        Assert.DoesNotContain("metformin", result.FilteredText);
    }

    [Theory]
    [InlineData("The patient has diabtes.", "diabetes", "low")]
    [InlineData("Diagnosis: hypertensoin.", "hypertension", "medium")]
    public void FuzzyFilter_DetectsNearMatchTerms(string input, string term, string level)
    {
        var filter = CreateFuzzyFilter(new[] { term }, level);
        var policy = CreatePolicy(new[] { term });
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.Dictionary, result.Spans[0].FilterType);
    }

    [Fact]
    public void FuzzyFilter_ExactTermStillDetected()
    {
        var filter = CreateFuzzyFilter(new[] { "diabetes" }, "low");
        var policy = CreatePolicy(new[] { "diabetes" });
        var result = filter.Filter(policy, "test", 0, "The patient has diabetes.");
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.Dictionary, result.Spans[0].FilterType);
    }

    [Theory]
    [InlineData("low",    "diabtes",  0.9)]
    [InlineData("medium", "dizbtes",  0.75)]
    [InlineData("high",   "dibzaes",  0.6)]
    public void FuzzyFilter_ConfidenceMatchesLevel(string level, string misspelled, double expectedConfidence)
    {
        var term = "diabetes";

        var filter = CreateFuzzyFilter(new[] { term }, level);
        var policy = CreatePolicy(new[] { term });
        var result = filter.Filter(policy, "test", 0, $"The patient has {misspelled}.");

        Assert.NotEmpty(result.Spans);
        Assert.Equal(expectedConfidence, result.Spans[0].Confidence);
    }

    [Fact]
    public void FuzzyFilter_DoesNotMatchDistantTerms()
    {
        var filter = CreateFuzzyFilter(new[] { "diabetes" }, "low");
        var policy = CreatePolicy(new[] { "diabetes" });
        var result = filter.Filter(policy, "test", 0, "The patient has xyzzy.");
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void DictionaryPolicyFilter_FuzzyAndLevelPropertiesDefault()
    {
        var dict = new Phileas.Policy.Filters.Dictionary
        {
            Name = "conditions",
            Terms = new List<string> { "diabetes" }
        };
        Assert.False(dict.Fuzzy);
        Assert.Equal("low", dict.Level);
    }

    [Fact]
    public void DictionaryPolicyFilter_FuzzyAndLevelPropertiesCanBeSet()
    {
        var dict = new Phileas.Policy.Filters.Dictionary
        {
            Name = "conditions",
            Terms = new List<string> { "diabetes" },
            Fuzzy = true,
            Level = "medium"
        };
        Assert.True(dict.Fuzzy);
        Assert.Equal("medium", dict.Level);
    }

    [Fact]
    public void FilterService_FuzzyDictionaryDetectsNearMatch()
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
                        Terms = new List<string> { "diabetes" },
                        Fuzzy = true,
                        Level = "low"
                    }
                }
            }
        };

        var result = FilterService.Filter(policy, "test", 0, "The patient has diabtes.");
        Assert.Contains("REDACTED", result.FilteredText);
    }
}
