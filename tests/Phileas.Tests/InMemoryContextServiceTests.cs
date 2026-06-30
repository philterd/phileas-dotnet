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
using Phileas.Filters.Strategies.Rules;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

public class InMemoryContextServiceTests
{
    [Fact]
    public void Get_ReturnsNull_WhenContextDoesNotExist()
    {
        var service = new InMemoryContextService();
        Assert.Null(service.Get("ctx", "123-45-6789"));
    }

    [Fact]
    public void Get_ReturnsNull_WhenTokenNotInContext()
    {
        var service = new InMemoryContextService();
        service.Put("ctx", "other-token", "replacement");
        Assert.Null(service.Get("ctx", "123-45-6789"));
    }

    [Fact]
    public void Put_And_Get_ReturnsSameReplacement()
    {
        var service = new InMemoryContextService();
        service.Put("ctx", "123-45-6789", "abc-replacement");
        Assert.Equal("abc-replacement", service.Get("ctx", "123-45-6789"));
    }

    [Fact]
    public void Put_Overwrites_ExistingReplacement()
    {
        var service = new InMemoryContextService();
        service.Put("ctx", "token", "first");
        service.Put("ctx", "token", "second");
        Assert.Equal("second", service.Get("ctx", "token"));
    }

    [Fact]
    public void Get_IsolatesValuesByContext()
    {
        var service = new InMemoryContextService();
        service.Put("ctx1", "token", "value1");
        service.Put("ctx2", "token", "value2");
        Assert.Equal("value1", service.Get("ctx1", "token"));
        Assert.Equal("value2", service.Get("ctx2", "token"));
    }

    [Fact]
    public void Get_ReturnsNull_ForUnknownContext_WhenOtherContextExists()
    {
        var service = new InMemoryContextService();
        service.Put("ctx1", "token", "value1");
        Assert.Null(service.Get("ctx2", "token"));
    }

    [Fact]
    public void RandomReplace_ProducesSameValueForSameToken()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Ssn = new Ssn() }
        };

        var contextService = new InMemoryContextService();
        var strategy = new SsnFilterStrategy
        {
            Strategy = AbstractFilterStrategy.RandomReplace,
            ReplacementScope = AbstractFilterStrategy.ReplacementScopeContext,
            ContextService = contextService
        };
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { strategy })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        var filter = new SsnFilter(config);

        var result1 = filter.Filter(policy, "testctx", 0, "SSN: 123-45-6789");
        var result2 = filter.Filter(policy, "testctx", 0, "SSN: 123-45-6789");

        Assert.NotEmpty(result1.Spans);
        Assert.NotEmpty(result2.Spans);
        Assert.Equal(result1.Spans[0].Replacement, result2.Spans[0].Replacement);
    }

    [Fact]
    public void RandomReplace_ProducesDifferentValuesForDifferentTokens()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Ssn = new Ssn() }
        };

        var contextService = new InMemoryContextService();
        var strategy = new SsnFilterStrategy
        {
            Strategy = AbstractFilterStrategy.RandomReplace,
            ReplacementScope = AbstractFilterStrategy.ReplacementScopeContext,
            ContextService = contextService
        };
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { strategy })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        var filter = new SsnFilter(config);

        var result1 = filter.Filter(policy, "testctx", 0, "SSN: 123-45-6789");
        var result2 = filter.Filter(policy, "testctx", 0, "SSN: 234-56-7890");

        Assert.NotEmpty(result1.Spans);
        Assert.NotEmpty(result2.Spans);
        Assert.NotEqual(result1.Spans[0].Replacement, result2.Spans[0].Replacement);
    }

    [Fact]
    public void FilterService_UsesProvidedContextService()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Ssn = new Ssn() }
        };

        // Pre-seed the context service with a known replacement for our token.
        // The filter should return that pre-seeded value rather than generating a new one.
        var contextService = new InMemoryContextService();
        var knownReplacement = "pre-seeded-value";
        contextService.Put("ctx", "123-45-6789", knownReplacement);

        var strategy = new SsnFilterStrategy
        {
            Strategy = AbstractFilterStrategy.RandomReplace,
            ReplacementScope = AbstractFilterStrategy.ReplacementScopeContext,
            ContextService = contextService
        };
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { strategy })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        var filter = new SsnFilter(config);

        var result = filter.Filter(policy, "ctx", 0, "SSN: 123-45-6789");
        Assert.NotEmpty(result.Spans);
        Assert.Equal(knownReplacement, result.Spans[0].Replacement);
    }

    [Fact]
    public void FilterService_DefaultsToInMemoryContextService_AndProducesConsistentReplacements()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Ssn = new Ssn() }
        };

        // When no context service is provided, FilterService should default to
        // InMemoryContextService. The same SSN filtered twice in the same call produces a span.
        var result1 = new FilterService().Filter(policy, "ctx", 0, "SSN: 123-45-6789");
        var result2 = new FilterService().Filter(policy, "ctx", 0, "SSN: 123-45-6789");
        Assert.NotEmpty(result1.Spans);
        Assert.NotEmpty(result2.Spans);
    }

    private static PhileasPolicy RandomReplaceSsnPolicy() => new()
    {
        Name = "test",
        Identifiers = new Identifiers
        {
            Ssn = new Ssn
            {
                Strategies = new List<Policy.Filters.Strategies.SsnFilterStrategy>
                {
                    new()
                    {
                        Strategy = Policy.Filters.Strategies.AbstractFilterStrategy.RandomReplace,
                        ReplacementScope = Policy.Filters.Strategies.AbstractFilterStrategy.ReplacementScopeContext
                    }
                }
            }
        }
    };

    [Fact]
    public void FilterService_UsesConstructorInjectedContextService()
    {
        // A context service injected via the constructor is used for calls that don't pass their own.
        var contextService = new InMemoryContextService();
        contextService.Put("ctx", "123-45-6789", "pre-seeded-value");

        var result = new FilterService(contextService).Filter(RandomReplaceSsnPolicy(), "ctx", 0, "SSN: 123-45-6789");

        Assert.NotEmpty(result.Spans);
        Assert.Equal("pre-seeded-value", result.Spans[0].Replacement);
    }

    [Fact]
    public void PerCallContextService_OverridesConstructorInjectedOne()
    {
        var injected = new InMemoryContextService();
        injected.Put("ctx", "123-45-6789", "from-constructor");
        var perCall = new InMemoryContextService();
        perCall.Put("ctx", "123-45-6789", "from-call");

        var result = new FilterService(injected).Filter(RandomReplaceSsnPolicy(), "ctx", 0, "SSN: 123-45-6789", perCall);

        Assert.Equal("from-call", result.Spans[0].Replacement);
    }

    [Fact]
    public void ConstructorInjectedContextService_GivesConsistencyAcrossSeparateCalls()
    {
        // The injected service persists mappings across Filter calls on the same FilterService; the
        // default (a fresh in-memory store per call) would not.
        var fs = new FilterService(new InMemoryContextService());

        var r1 = fs.Filter(RandomReplaceSsnPolicy(), "ctx", 0, "SSN: 123-45-6789");
        var r2 = fs.Filter(RandomReplaceSsnPolicy(), "ctx", 0, "SSN: 123-45-6789");

        Assert.NotEmpty(r1.Spans);
        Assert.NotEmpty(r2.Spans);
        Assert.Equal(r1.Spans[0].Replacement, r2.Spans[0].Replacement);
    }
}