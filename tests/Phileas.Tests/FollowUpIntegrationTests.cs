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
using Phileas.Services;
using Xunit;
using PolicyIdentifiers = Phileas.Policy.Identifiers;
using PhileasPolicy = Phileas.Policy.Policy;
using RuntimeStrategy = Phileas.Filters.AbstractFilterStrategy;
using RZipStrategy = Phileas.Filters.Strategies.Rules.ZipCodeFilterStrategy;
using RDateStrategy = Phileas.Filters.Strategies.Rules.DateFilterStrategy;
using RSsnStrategy = Phileas.Filters.Strategies.Rules.SsnFilterStrategy;
using PEmailStrategy = Phileas.Policy.Filters.Strategies.EmailAddressFilterStrategy;

namespace Phileas.Tests;

/// <summary>
///     Coverage for the parity follow-ups: document-scoped ignored terms, the DOCUMENT/CONTEXT
///     replacement scope, the population condition, <c>onlyValidDates</c>, and ZIP code validation.
/// </summary>
public class FollowUpIntegrationTests
{
    private static ZipCodeFilter ZipFilter(bool requireDelimiter = false, bool validate = false)
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<RuntimeStrategy> { new RZipStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new ZipCodeFilter(config, requireDelimiter, validate);
    }

    private static DateFilter DateFilterFor(bool onlyValidDates)
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<RuntimeStrategy> { new RDateStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new DateFilter(config, onlyValidDates);
    }

    private static PhileasPolicy BarePolicy()
    {
        // FindSpans only runs a filter when the policy declares the matching identifier, so enable the
        // identifiers exercised by the direct-filter tests below.
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new PolicyIdentifiers
            {
                Ssn = new Ssn(),
                Date = new Date(),
                ZipCode = new ZipCode()
            }
        };
    }

    // ----- Global (document-scoped) ignored terms -----

    [Fact]
    public void GlobalIgnored_RemovesMatchingSpanRegardlessOfFilter()
    {
        var policy = new PhileasPolicy
        {
            Ignored = new List<Ignored> { new() { Terms = new List<string> { "test@something.com" } } },
            Identifiers = new PolicyIdentifiers
            {
                EmailAddress = new EmailAddress { Strategies = new List<PEmailStrategy> { new() } }
            }
        };

        var response = new FilterService().Filter(policy, "context", 0, "Email me at test@something.com please.");

        Assert.Empty(response.Spans);
        Assert.Equal("Email me at test@something.com please.", response.FilteredText);
    }

    [Fact]
    public void GlobalIgnored_IsCaseInsensitiveByDefault()
    {
        var policy = new PhileasPolicy
        {
            Ignored = new List<Ignored> { new() { Terms = new List<string> { "TEST@SOMETHING.COM" } } },
            Identifiers = new PolicyIdentifiers
            {
                EmailAddress = new EmailAddress { Strategies = new List<PEmailStrategy> { new() } }
            }
        };

        var response = new FilterService().Filter(policy, "context", 0, "Reach test@something.com now.");

        Assert.Empty(response.Spans);
    }

    [Fact]
    public void GlobalIgnored_CaseSensitive_KeepsSpanWhenCaseDiffers()
    {
        var policy = new PhileasPolicy
        {
            Ignored = new List<Ignored>
                { new() { CaseSensitive = true, Terms = new List<string> { "TEST@SOMETHING.COM" } } },
            Identifiers = new PolicyIdentifiers
            {
                EmailAddress = new EmailAddress { Strategies = new List<PEmailStrategy> { new() } }
            }
        };

        var response = new FilterService().Filter(policy, "context", 0, "Reach test@something.com now.");

        Assert.Single(response.Spans);
    }

    // ----- Replacement scope -----

    [Fact]
    public void ReplacementScope_DocumentIsTheDefault()
    {
        Assert.Equal(RuntimeStrategy.ReplacementScopeDocument, new RSsnStrategy().ReplacementScope);
        Assert.Equal(RuntimeStrategy.ReplacementScopeDocument, new RSsnStrategy().ReplacementScope);
    }

    [Fact]
    public void ReplacementScope_DocumentDoesNotReuseContextReplacement()
    {
        var contextService = new InMemoryContextService();
        contextService.Put("ctx", "123-45-6789", "pre-seeded-value");

        var strategy = new RSsnStrategy
        {
            Strategy = RuntimeStrategy.RandomReplace,
            ReplacementScope = RuntimeStrategy.ReplacementScopeDocument,
            ContextService = contextService
        };
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<RuntimeStrategy> { strategy })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        var filter = new SsnFilter(config);

        var result = filter.Filter(BarePolicy(), "ctx", 0, "SSN: 123-45-6789");

        Assert.Single(result.Spans);
        // DOCUMENT scope anonymizes directly and ignores the pre-seeded context value.
        Assert.NotEqual("pre-seeded-value", result.Spans[0].Replacement);
    }

    // ----- onlyValidDates -----

    [Fact]
    public void OnlyValidDates_False_KeepsWellFormedButInvalidDate()
    {
        var result = DateFilterFor(false).Filter(BarePolicy(), "ctx", 0, "Date: 02-31-2019");
        Assert.Single(result.Spans);
    }

    [Fact]
    public void OnlyValidDates_True_DropsInvalidNumericDate()
    {
        var result = DateFilterFor(true).Filter(BarePolicy(), "ctx", 0, "Date: 02-31-2019");
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void OnlyValidDates_True_KeepsValidNumericDate()
    {
        var result = DateFilterFor(true).Filter(BarePolicy(), "ctx", 0, "Date: 12-31-2000");
        Assert.Single(result.Spans);
    }

    [Fact]
    public void OnlyValidDates_True_KeepsMonthNameDate()
    {
        var result = DateFilterFor(true).Filter(BarePolicy(), "ctx", 0, "Born on January 15, 1990");
        Assert.Single(result.Spans);
    }

    // ----- ZIP code validate / requireDelimiter -----

    [Fact]
    public void ZipValidate_DropsZipNotInCensus()
    {
        var result = ZipFilter(validate: true).Filter(BarePolicy(), "ctx", 0, "ZIP: 00000");
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void ZipValidate_KeepsZipInCensus()
    {
        var result = ZipFilter(validate: true).Filter(BarePolicy(), "ctx", 0, "ZIP: 90210");
        Assert.Single(result.Spans);
    }

    [Fact]
    public void ZipValidate_Disabled_KeepsZipNotInCensus()
    {
        var result = ZipFilter(validate: false).Filter(BarePolicy(), "ctx", 0, "ZIP: 00000");
        Assert.Single(result.Spans);
    }

    [Fact]
    public void ZipRequireDelimiter_DoesNotMatchUndelimitedPlusFour()
    {
        // An undelimited 9-digit run has no word boundary after the 5th digit, so the delimiter-requiring
        // pattern does not match it at all.
        var result = ZipFilter(requireDelimiter: true).Filter(BarePolicy(), "ctx", 0, "ZIP 902101234 here");
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void ZipRequireDelimiter_MatchesDelimitedPlusFour()
    {
        var result = ZipFilter(requireDelimiter: true).Filter(BarePolicy(), "ctx", 0, "ZIP 90210-1234 here");
        Assert.Single(result.Spans);
        Assert.Equal("90210-1234", result.Spans[0].Text);
    }

    [Fact]
    public void ZipNoDelimiter_MatchesUndelimitedPlusFour()
    {
        var result = ZipFilter(requireDelimiter: false).Filter(BarePolicy(), "ctx", 0, "ZIP 902101234 here");
        Assert.Single(result.Spans);
        Assert.Equal("902101234", result.Spans[0].Text);
    }
}
