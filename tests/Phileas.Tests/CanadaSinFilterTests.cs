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

using Phileas.Data.Generators;
using Phileas.Filters;
using Phileas.Filters.Rules.Regex.RegexFilters;
using Phileas.Filters.Strategies.Rules;
using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Phileas.Services.Anonymization;
using Phileas.Services.Validators;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

// Every number here is invented. 046 454 286 is the example from philterd/phileas-dotnet#157.
public class CanadaSinFilterTests
{
    private static CanadaSinFilter CreateFilter(bool onlyValidPrefixes = false)
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new CanadaSinFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new CanadaSinFilter(config, onlyValidPrefixes);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { CanadaSin = new CanadaSin() }
        };
    }

    private static Filtered Filter(string input, bool onlyValidPrefixes = false)
    {
        return CreateFilter(onlyValidPrefixes).Filter(CreatePolicy(), "test", 0, input);
    }

    // Filters with the policy as a document would be: deserialized, so schema validation runs.
    private static TextFilterResult FilterWithPolicy(string identifiersJson, string input)
    {
        var policy = PolicySerializer.DeserializeFromJson("{\"identifiers\": {" + identifiersJson + "}}");
        return new FilterService().Filter(policy, "ctx", 0, input);
    }

    [Theory]
    [InlineData("046454286")]
    [InlineData("046 454 286")]
    [InlineData("046-454-286")]
    public void Filter_DetectsEachWrittenForm(string sin)
    {
        var result = Filter("SIN: " + sin + " on file");
        var span = Assert.Single(result.Spans);
        Assert.Equal(sin, span.Text);
        Assert.Equal(FilterType.CanadaSin, span.FilterType);
        Assert.Equal("canada-sin", span.FilterType.GetFilterTypeName());
        Assert.Equal(5, span.CharacterStart);
        Assert.Equal(5 + sin.Length, span.CharacterEnd);
    }

    [Theory]
    [InlineData("123 456 789")]
    [InlineData("123-456-789")]
    [InlineData("123456789")]
    [InlineData("046 454 287")] // the specimen with its check digit changed
    public void Filter_DoesNotDetectLuhnInvalidValue(string sin)
    {
        Assert.Empty(Filter("SIN: " + sin).Spans);
    }

    [Fact]
    public void Filter_DetectsTemporaryResidentSinBeginningWith9()
    {
        Assert.Single(Filter("NAS : 912 345 675").Spans);
    }

    [Theory]
    [InlineData("046-454-\n286")] // wrapped after the second hyphen
    [InlineData("046-\n454-286")] // wrapped after the first hyphen
    [InlineData("046-454-\r\n286")] // CRLF break
    [InlineData("046-454-\n    286")] // indented continuation line
    [InlineData("046‑454‑\n286")] // a hyphen substitute preceding the break
    public void Filter_DetectsSinWrappedAcrossALineBreak(string sin)
    {
        var span = Assert.Single(Filter("SIN " + sin + " end").Spans);
        Assert.Equal(sin, span.Text);
    }

    [Theory]
    [InlineData("046\n454\n286")] // a line break with no hyphen before it
    [InlineData("046-45\n4-286")] // the break falls inside a digit group
    [InlineData("046  454  286")] // more than one horizontal space, and no hyphen
    [InlineData("046 - 454 - 286")] // whitespace before the hyphen
    [InlineData("1046454286")] // inside a longer digit run
    [InlineData("046454286A")] // butted against a letter
    [InlineData("０４６４５４２８６")] // fullwidth digits
    public void Filter_DoesNotDetectExcludedForms(string input)
    {
        Assert.Empty(Filter(input).Spans);
    }

    [Theory]
    [InlineData("046 454 286")] // leading 0
    [InlineData("800 123 457")] // leading 8, the business number range
    public void Filter_DetectsLeading0Or8ByDefault(string sin)
    {
        Assert.Single(Filter("SIN: " + sin).Spans);
        Assert.Empty(Filter("SIN: " + sin, onlyValidPrefixes: true).Spans);
    }

    [Theory]
    [InlineData("123 456 782")]
    [InlineData("271 835 464")]
    [InlineData("555 123 454")]
    [InlineData("714 285 731")]
    [InlineData("912 345 675")]
    public void Filter_DetectsOtherLeadingDigitsEitherWay(string sin)
    {
        Assert.Single(Filter("SIN: " + sin).Spans);
        Assert.Single(Filter("SIN: " + sin, onlyValidPrefixes: true).Spans);
    }

    [Fact]
    public void Filter_DetectsRepeatedSinsInOneDocument()
    {
        var result = Filter("Employee 046-454-286, spouse 271 835 464.");
        Assert.Equal(new[] { "046-454-286", "271 835 464" }, result.Spans.Select(span => span.Text));
    }

    [Fact]
    public void Policy_BindsCanadaSinWithItsStrategiesAndOption()
    {
        var policy = PolicySerializer.DeserializeFromJson(
            "{\"identifiers\": {\"canadaSin\": {\"onlyValidPrefixes\": true, " +
            "\"canadaSinFilterStrategies\": [{\"strategy\": \"MASK\"}]}}}");

        Assert.NotNull(policy.Identifiers.CanadaSin);
        Assert.True(policy.Identifiers.CanadaSin!.OnlyValidPrefixes);
        Assert.Equal("MASK", Assert.Single(policy.Identifiers.CanadaSin.Strategies!).Strategy);
        Assert.True(policy.Identifiers.HasFilter(FilterType.CanadaSin));

        var json = PolicySerializer.SerializeToJson(policy);
        Assert.Contains("\"canadaSin\"", json);
        Assert.Contains("\"canadaSinFilterStrategies\"", json);
        PolicySerializer.DeserializeFromJson(json);
    }

    [Fact]
    public void Policy_OnlyValidPrefixesDefaultsToFalse()
    {
        Assert.False(new CanadaSin().OnlyValidPrefixes);
    }

    [Fact]
    public void FilterService_HonorsOnlyValidPrefixes()
    {
        Assert.Single(FilterWithPolicy("\"canadaSin\": {}", "SIN 800 123 457").Spans);
        Assert.Empty(FilterWithPolicy("\"canadaSin\": {\"onlyValidPrefixes\": true}", "SIN 800 123 457").Spans);
    }

    [Fact]
    public void FilterService_DisabledFilterDetectsNothing()
    {
        Assert.Empty(FilterWithPolicy("\"canadaSin\": {\"enabled\": false}", "SIN 046 454 286").Spans);
    }

    [Theory]
    [InlineData("046 454 286")]
    [InlineData("046-454-286")]
    public void FilterService_WithSsnEnabled_FormsSsnCannotMatchStaySin(string sin)
    {
        // SSN groups are 3-2-4, so a separated SIN is never SSN-shaped and reaches disambiguation unopposed.
        var span = Assert.Single(FilterWithPolicy("\"canadaSin\": {}, \"ssn\": {}", "Number " + sin).Spans);
        Assert.Equal(FilterType.CanadaSin, span.FilterType);
    }

    [Fact]
    public void FilterService_WithSsnEnabled_AnUnformattedRunMatchingBothYieldsOneSpan()
    {
        // 271835464 is a valid SSN shape and a Luhn-valid SIN. Which type wins is the existing
        // disambiguation's call; this pins only that the two are resolved to one span over the value.
        var result = FilterWithPolicy("\"canadaSin\": {}, \"ssn\": {}", "Number 271835464");
        var span = Assert.Single(result.Spans);
        Assert.Equal("271835464", span.Text);
        Assert.Contains(span.FilterType, new[] { FilterType.CanadaSin, FilterType.Ssn });
    }

    [Fact]
    public void FilterService_WithSsnEnabled_PriorityDecidesAnUnformattedRun()
    {
        // Equal length and confidence, so the filters' priority settles it, ahead of filter order.
        var result = FilterWithPolicy("\"canadaSin\": {\"priority\": 1}, \"ssn\": {}", "Number 271835464");
        Assert.Equal(FilterType.CanadaSin, Assert.Single(result.Spans).FilterType);
    }

    [Theory]
    [InlineData("REDACT", "{{{REDACTED-canada-sin}}}")]
    [InlineData("MASK", "***********")]
    [InlineData("LAST_4", "-286")]
    [InlineData("STATIC_REPLACE\", \"staticReplacement\": \"[SIN]", "[SIN]")]
    [InlineData("TRUNCATE", "046-*******")]
    public void FilterService_AppliesDeterministicStrategies(string strategy, string expected)
    {
        var result = FilterWithPolicy(
            "\"canadaSin\": {\"canadaSinFilterStrategies\": [{\"strategy\": \"" + strategy + "\"}]}",
            "SIN 046-454-286");
        Assert.Equal(expected, Assert.Single(result.Spans).Replacement);
    }

    [Theory]
    [InlineData("HASH_SHA256_REPLACE", @"^[0-9a-f]{64}$")]
    [InlineData("ABBREVIATE", @"^0$")]
    [InlineData("CRYPTO_REPLACE", @"^\{\{[A-Za-z0-9+/=]+\}\}$")]
    [InlineData("FPE_ENCRYPT_REPLACE", @"^[0-9]{3}-[0-9]{3}-[0-9]{3}$")]
    [InlineData("MAP_REPLACE\", \"mappings\": {\"046-454-286\": \"271-835-464\"}, \"fallbackStrategy\": \"REDACT",
        @"^271-835-464$")]
    public void FilterService_AppliesTransformingStrategies(string strategy, string expected)
    {
        var policy = PolicySerializer.DeserializeFromJson(
            "{\"identifiers\": {\"canadaSin\": {\"canadaSinFilterStrategies\": [{\"strategy\": \"" + strategy +
            "\"}]}}}");
        policy.Crypto = new Crypto { Key = "9EE7A356FDFE43F069500B0086758346E66D8583E0CE1CFCA04E50F67ECCE5D1" };
        policy.Fpe = new Fpe { Key = "EF4359D8D580AA4F7F036D6F04FC6A94", Tweak = "D8E7920AFA330A73" };

        var span = Assert.Single(new FilterService().Filter(policy, "ctx", 0, "SIN 046-454-286").Spans);
        Assert.Matches(expected, span.Replacement);
        Assert.NotEqual("046-454-286", span.Replacement);
    }

    [Theory]
    [InlineData("046454286", @"^[0-9]{9}$")]
    [InlineData("046 454 286", @"^[0-9]{3} [0-9]{3} [0-9]{3}$")]
    [InlineData("046-454-286", @"^[0-9]{3}-[0-9]{3}-[0-9]{3}$")]
    public void FilterService_RandomReplaceProducesLuhnValidSinInTheSameFormat(string sin, string shape)
    {
        for (var i = 0; i < 25; i++)
        {
            var replacement = Assert.Single(FilterWithPolicy(
                "\"canadaSin\": {\"canadaSinFilterStrategies\": [{\"strategy\": \"RANDOM_REPLACE\"}]}",
                "SIN " + sin).Spans).Replacement;

            Assert.Matches(shape, replacement);
            Assert.True(LuhnValidator.IsValid(replacement), replacement);
            Assert.NotEqual(sin, replacement);
        }
    }

    [Fact]
    public void Factory_MapsCanadaSinToItsService()
    {
        Assert.IsType<CanadaSinAnonymizationService>(
            AnonymizationServiceFactory.Create(FilterType.CanadaSin, new InMemoryContextService(), new Random()));
    }

    [Fact]
    public void Generator_ProducesLuhnValidSinsWithAnIssuedLeadingDigit()
    {
        var generator = new CanadaSinGenerator(new Random(157));
        for (var i = 0; i < 1000; i++)
        {
            var value = generator.Random();
            Assert.Matches(@"^[1-79][0-9]{2}-[0-9]{3}-[0-9]{3}$", value);
            Assert.True(LuhnValidator.IsValid(value), value);
        }
    }

    [Fact]
    public void Generator_PoolSize()
    {
        Assert.Equal(80_000_000L, new CanadaSinGenerator(new Random()).PoolSize());
    }
}
