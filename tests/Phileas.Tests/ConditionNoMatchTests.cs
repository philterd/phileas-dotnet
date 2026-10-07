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

using Phileas.Filters;
using Phileas.Filters.Rules.Regex.RegexFilters;
using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
///     When every strategy on a filter has a condition and none is satisfied, the value is left unchanged and
///     no span is reported for it. A filter with no strategies still redacts. See philterd/phileas-dotnet#145
///     and philterd/phisql#57.
/// </summary>
public class ConditionNoMatchTests
{
    private static TextFilterResult Filter(Identifiers identifiers, string input) =>
        new FilterService().Filter(new PhileasPolicy { Name = "p", Identifiers = identifiers }, "ctx", 0, input);

    private static Ssn SsnWith(params SsnFilterStrategy[] strategies) =>
        new() { Strategies = strategies.ToList() };

    [Fact]
    public void OnlyTheValueTheConditionMatchesIsReplaced()
    {
        // The case from the issue: the Java and Python ports leave the first SSN unchanged.
        var ssn = SsnWith(new SsnFilterStrategy
        {
            Strategy = "STATIC_REPLACE", StaticReplacement = "MATCHED", Condition = "token == \"555-55-1234\""
        });

        var result = Filter(new Identifiers { Ssn = ssn }, "ssn 123-45-6789 and 555-55-1234");

        Assert.Equal("ssn 123-45-6789 and MATCHED", result.FilteredText);
        Assert.Equal("555-55-1234", Assert.Single(result.Spans).Text);
    }

    [Fact]
    public void AllStrategiesConditional_NoneMatching_LeavesTheTextAndReportsNoSpan()
    {
        var ssn = SsnWith(
            new SsnFilterStrategy { Strategy = "REDACT", Condition = "token startswith \"999\"" },
            new SsnFilterStrategy { Strategy = "MASK", Condition = "context == \"other\"" });

        var result = Filter(new Identifiers { Ssn = ssn }, "ssn 123-45-6789");

        Assert.Equal("ssn 123-45-6789", result.FilteredText);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void ALaterUnconditionalStrategy_StillApplies()
    {
        var ssn = SsnWith(
            new SsnFilterStrategy { Strategy = "STATIC_REPLACE", StaticReplacement = "MATCHED", Condition = "token == \"555-55-1234\"" },
            new SsnFilterStrategy { Strategy = "REDACT" });

        var result = Filter(new Identifiers { Ssn = ssn }, "ssn 123-45-6789 and 555-55-1234");

        Assert.Equal("ssn {{{REDACTED-ssn}}} and MATCHED", result.FilteredText);
        Assert.Equal(2, result.Spans.Count);
    }

    [Fact]
    public void AFilterWithNoStrategies_StillRedacts()
    {
        var result = Filter(new Identifiers { Ssn = new Ssn() }, "ssn 123-45-6789");

        Assert.Equal("ssn {{{REDACTED-ssn}}}", result.FilteredText);
        Assert.Single(result.Spans);
    }

    [Fact]
    public void AFilterBuiltWithAnEmptyStrategyList_StillRedacts()
    {
        // FilterService always gives a filter at least one strategy; a filter built directly may have none.
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<Phileas.Filters.AbstractFilterStrategy>())
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        var filter = new TrackingNumberFilter(config);

        var result = filter.Filter(new PhileasPolicy { Name = "p", Identifiers = new Identifiers { TrackingNumber = new TrackingNumber() } },
            "ctx", 0, "Ship 123456789012 today");

        Assert.Equal("{{{REDACTED-tracking-number}}}", Assert.Single(result.Spans).Replacement);
    }

    [Fact]
    public void CustomDictionary_NoConditionMatching_LeavesTheTerm()
    {
        var dictionary = new CustomDictionary
        {
            Terms = new List<string> { "Zephyrous", "Wanderlust" },
            Strategies = new List<CustomDictionaryFilterStrategy>
            {
                new() { Strategy = "REDACT", Condition = "token == \"Wanderlust\"" }
            }
        };

        var result = Filter(new Identifiers { CustomDictionaries = new List<CustomDictionary> { dictionary } },
            "Codename Zephyrous and Wanderlust.");

        Assert.Equal("Codename Zephyrous and {{{REDACTED-custom-dictionary}}}.", result.FilteredText);
        Assert.Equal("Wanderlust", Assert.Single(result.Spans).Text);
    }

    [Fact]
    public void FuzzyDictionary_AnExactMatchLeftUnchanged_IsNotReportedAsANearMatch()
    {
        // The exact match is left by its condition. The near-match scan covers the same text at a lower
        // confidence, and a condition satisfied only by that confidence must not pick it up instead.
        var dictionary = new CustomDictionary
        {
            Terms = new List<string> { "Zephyrous" },
            Fuzzy = true,
            Sensitivity = "high",
            Strategies = new List<CustomDictionaryFilterStrategy>
            {
                new() { Strategy = "REDACT", Condition = "confidence < 1.0" }
            }
        };

        var result = Filter(new Identifiers { CustomDictionaries = new List<CustomDictionary> { dictionary } },
            "Codename Zephyrous active.");

        Assert.Equal("Codename Zephyrous active.", result.FilteredText);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void PhoneNumber_NoConditionMatching_LeavesTheNumber()
    {
        var phone = new PhoneNumber
        {
            Strategies = new List<PhoneNumberFilterStrategy>
            {
                new() { Strategy = "REDACT", Condition = "token startswith \"(555)\"" }
            }
        };

        var result = Filter(new Identifiers { PhoneNumber = phone }, "Call (410) 555-0199 today.");

        Assert.Equal("Call (410) 555-0199 today.", result.FilteredText);
        Assert.Empty(result.Spans);
    }
}
