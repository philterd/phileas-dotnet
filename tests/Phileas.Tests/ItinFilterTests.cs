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
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

// Every number here is invented. 912-70-1234 is the example from philterd/phileas-dotnet#144.
public class ItinFilterTests
{
    private static ItinFilter CreateFilter(bool onlyValidRanges = false)
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new ItinFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new ItinFilter(config, onlyValidRanges);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Itin = new Itin() }
        };
    }

    private static Filtered Filter(string input, bool onlyValidRanges = false)
    {
        return CreateFilter(onlyValidRanges).Filter(CreatePolicy(), "test", 0, input);
    }

    // Filters with the policy as a document would be: deserialized, so schema validation runs.
    private static TextFilterResult FilterWithPolicy(string identifiersJson, string input)
    {
        var policy = PolicySerializer.DeserializeFromJson("{\"identifiers\": {" + identifiersJson + "}}");
        return new FilterService().Filter(policy, "ctx", 0, input);
    }

    [Theory]
    [InlineData("912-70-1234")]
    [InlineData("912 70 1234")]
    [InlineData("912701234")]
    public void Filter_DetectsEachWrittenForm(string itin)
    {
        var result = Filter("ITIN: " + itin + " on file");
        var span = Assert.Single(result.Spans);
        Assert.Equal(itin, span.Text);
        Assert.Equal(FilterType.Itin, span.FilterType);
        Assert.Equal("itin", span.FilterType.GetFilterTypeName());
        Assert.Equal(6, span.CharacterStart);
        Assert.Equal(6 + itin.Length, span.CharacterEnd);
    }

    [Theory]
    [InlineData("123-45-6789")]
    [InlineData("812-70-1234")]
    [InlineData("12-3456789")] // EIN shape
    public void Filter_DoesNotDetectValuesNotBeginningWith9(string value)
    {
        Assert.Empty(Filter("ID " + value).Spans);
    }

    [Theory]
    [InlineData("\u00AD")] // soft hyphen
    [InlineData("\u2011")] // non-breaking hyphen
    [InlineData("\u2013")] // en dash
    [InlineData("\u2212")] // minus sign
    [InlineData("\uFF0D")] // fullwidth hyphen-minus
    [InlineData("\t")] // tab
    [InlineData("\u00A0")] // non-breaking space
    [InlineData("- ")] // whitespace following a hyphen
    public void Filter_AcceptsTheSsnSeparators(string separator)
    {
        var itin = "912" + separator + "70" + separator + "1234";
        Assert.Equal(itin, Assert.Single(Filter("ITIN " + itin).Spans).Text);
    }

    [Theory]
    [InlineData("912-70-\n1234")] // wrapped after the second hyphen
    [InlineData("912-\n70-1234")] // wrapped after the first hyphen
    [InlineData("912-70-\r\n    1234")] // CRLF break and an indented continuation line
    [InlineData("912‑70‑\n1234")] // a hyphen substitute preceding the break
    public void Filter_DetectsItinWrappedAcrossALineBreak(string itin)
    {
        var span = Assert.Single(Filter("ITIN " + itin + " end").Spans);
        Assert.Equal(itin, span.Text);
    }

    [Theory]
    [InlineData("912\n70\n1234")] // a line break with no hyphen before it
    [InlineData("912-70-12\n34")] // the break falls inside a digit group
    [InlineData("912  70  1234")] // more than one horizontal space, and no hyphen
    [InlineData("912 - 70 - 1234")] // whitespace before the hyphen
    [InlineData("1912701234")] // inside a longer digit run
    [InlineData("912701234A")] // butted against a letter
    [InlineData("９１２－７０－１２３４")] // fullwidth digits
    public void Filter_DoesNotDetectExcludedForms(string input)
    {
        Assert.Empty(Filter(input).Spans);
    }

    [Theory]
    [InlineData("912-40-1234")] // below every range
    [InlineData("912-66-1234")] // between 65 and 70
    [InlineData("912-89-1234")] // reserved for other programs
    [InlineData("912-00-0000")]
    public void Filter_OutOfRangeValueIsDetectedByDefaultAndDroppedWithOnlyValidRanges(string itin)
    {
        Assert.Single(Filter("ITIN " + itin).Spans);
        Assert.Empty(Filter("ITIN " + itin, onlyValidRanges: true).Spans);
    }

    [Theory]
    [InlineData("912-93-1234")]
    [InlineData("912931234")]
    public void Filter_AtinIsDetectedAsItinByDefaultAndDroppedWithOnlyValidRanges(string atin)
    {
        var span = Assert.Single(Filter("ATIN " + atin).Spans);
        Assert.Equal(FilterType.Itin, span.FilterType);
        Assert.Empty(Filter("ATIN " + atin, onlyValidRanges: true).Spans);
    }

    [Theory]
    [InlineData("912-50-1234")]
    [InlineData("912-65-1234")]
    [InlineData("912-70-1234")]
    [InlineData("912-88-1234")]
    [InlineData("912-90-1234")]
    [InlineData("912-92-1234")]
    [InlineData("912-94-1234")]
    [InlineData("912 99 1234")]
    [InlineData("912751234")]
    public void Filter_InRangeValueIsDetectedEitherWay(string itin)
    {
        Assert.Single(Filter("ITIN " + itin).Spans);
        Assert.Single(Filter("ITIN " + itin, onlyValidRanges: true).Spans);
    }

    [Theory]
    [InlineData(49, false)]
    [InlineData(50, true)]
    [InlineData(65, true)]
    [InlineData(66, false)]
    [InlineData(69, false)]
    [InlineData(70, true)]
    [InlineData(88, true)]
    [InlineData(89, false)]
    [InlineData(90, true)]
    [InlineData(92, true)]
    [InlineData(93, false)]
    [InlineData(94, true)]
    [InlineData(99, true)]
    public void IsInValidRange_MatchesTheIrsRanges(int group, bool expected)
    {
        Assert.Equal(expected, ItinFilter.IsInValidRange($"912-{group:D2}-1234"));
    }

    [Fact]
    public void FilterService_ItinAndSsnAreReportedSeparately()
    {
        var result = FilterWithPolicy("\"itin\": {}, \"ssn\": {}, \"ein\": {}",
            "ITIN 912-70-1234 and SSN 123-45-6789");

        Assert.Equal(2, result.Spans.Count);
        Assert.Equal(FilterType.Itin, result.Spans.Single(span => span.Text == "912-70-1234").FilterType);
        Assert.Equal(FilterType.Ssn, result.Spans.Single(span => span.Text == "123-45-6789").FilterType);
    }

    [Theory]
    [InlineData("912-70-1234")]
    [InlineData("912 70 1234")]
    [InlineData("912701234")]
    [InlineData("912-70-\n1234")]
    [InlineData("999-99-9999")]
    [InlineData("900-50-0001")]
    public void SsnFilter_NeverReportsAValueBeginningWith9(string value)
    {
        Assert.Empty(FilterWithPolicy("\"ssn\": {}", "SSN " + value).Spans);
    }

    [Fact]
    public void Policy_BindsItinWithItsStrategiesAndOption()
    {
        var policy = PolicySerializer.DeserializeFromJson(
            "{\"identifiers\": {\"itin\": {\"onlyValidRanges\": true, " +
            "\"itinFilterStrategies\": [{\"strategy\": \"LAST_4\"}]}}}");

        Assert.NotNull(policy.Identifiers.Itin);
        Assert.True(policy.Identifiers.Itin!.OnlyValidRanges);
        Assert.Equal("LAST_4", Assert.Single(policy.Identifiers.Itin.Strategies!).Strategy);
        Assert.True(policy.Identifiers.HasFilter(FilterType.Itin));

        var json = PolicySerializer.SerializeToJson(policy);
        Assert.Contains("\"itin\"", json);
        Assert.Contains("\"itinFilterStrategies\"", json);
        PolicySerializer.DeserializeFromJson(json);
    }

    [Fact]
    public void Policy_OnlyValidRangesDefaultsToFalse()
    {
        Assert.False(new Itin().OnlyValidRanges);
    }

    [Fact]
    public void FilterService_HonorsOnlyValidRanges()
    {
        Assert.Single(FilterWithPolicy("\"itin\": {}", "ITIN 912-93-1234").Spans);
        Assert.Empty(FilterWithPolicy("\"itin\": {\"onlyValidRanges\": true}", "ITIN 912-93-1234").Spans);
    }

    [Fact]
    public void FilterService_DisabledFilterDetectsNothing()
    {
        Assert.Empty(FilterWithPolicy("\"itin\": {\"enabled\": false}", "ITIN 912-70-1234").Spans);
    }

    [Theory]
    [InlineData("REDACT", @"^\{\{\{REDACTED-itin\}\}\}$")]
    [InlineData("MASK", @"^\*{11}$")]
    [InlineData("LAST_4", @"^1234$")]
    [InlineData("STATIC_REPLACE\", \"staticReplacement\": \"[ITIN]", @"^\[ITIN\]$")]
    [InlineData("TRUNCATE", @"^912-\*{7}$")]
    [InlineData("HASH_SHA256_REPLACE", @"^[0-9a-f]{64}$")]
    [InlineData("ABBREVIATE", @"^9$")]
    [InlineData("CRYPTO_REPLACE", @"^\{\{[A-Za-z0-9+/=]+\}\}$")]
    [InlineData("FPE_ENCRYPT_REPLACE", @"^[0-9]{3}-[0-9]{2}-[0-9]{4}$")]
    [InlineData("MAP_REPLACE\", \"mappings\": {\"912-70-1234\": \"987-65-4321\"}, \"fallbackStrategy\": \"REDACT",
        @"^987-65-4321$")]
    public void FilterService_AppliesEachStrategy(string strategy, string expected)
    {
        var policy = PolicySerializer.DeserializeFromJson(
            "{\"identifiers\": {\"itin\": {\"itinFilterStrategies\": [{\"strategy\": \"" + strategy + "\"}]}}}");
        policy.Crypto = new Crypto { Key = "9EE7A356FDFE43F069500B0086758346E66D8583E0CE1CFCA04E50F67ECCE5D1" };
        policy.Fpe = new Fpe { Key = "EF4359D8D580AA4F7F036D6F04FC6A94", Tweak = "D8E7920AFA330A73" };

        var span = Assert.Single(new FilterService().Filter(policy, "ctx", 0, "ITIN 912-70-1234").Spans);
        Assert.Matches(expected, span.Replacement);
        Assert.NotEqual("912-70-1234", span.Replacement);
    }

    [Theory]
    [InlineData("912701234", @"^9[0-9]{8}$")]
    [InlineData("912 70 1234", @"^9[0-9]{2} [0-9]{2} [0-9]{4}$")]
    [InlineData("912-70-1234", @"^9[0-9]{2}-[0-9]{2}-[0-9]{4}$")]
    public void FilterService_RandomReplaceProducesAnInRangeItinInTheSameFormat(string itin, string shape)
    {
        for (var i = 0; i < 25; i++)
        {
            var replacement = Assert.Single(FilterWithPolicy(
                "\"itin\": {\"itinFilterStrategies\": [{\"strategy\": \"RANDOM_REPLACE\"}]}",
                "ITIN " + itin).Spans).Replacement;

            Assert.Matches(shape, replacement);
            Assert.True(ItinFilter.IsInValidRange(replacement), replacement);
            Assert.NotEqual(itin, replacement);
        }
    }

    [Fact]
    public void Factory_MapsItinToItsService()
    {
        Assert.IsType<ItinAnonymizationService>(
            AnonymizationServiceFactory.Create(FilterType.Itin, new InMemoryContextService(), new Random()));
    }

    [Fact]
    public void Generator_ProducesInRangeItins()
    {
        var generator = new ItinGenerator(new Random(144));
        for (var i = 0; i < 1000; i++)
        {
            var value = generator.Random();
            Assert.Matches(@"^9[0-9]{2}-[0-9]{2}-[0-9]{4}$", value);
            Assert.True(ItinFilter.IsInValidRange(value), value);
        }
    }

    [Fact]
    public void Generator_PoolSize()
    {
        Assert.Equal(44_000_000L, new ItinGenerator(new Random()).PoolSize());
    }
}
