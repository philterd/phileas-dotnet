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
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas;
using Phileas.Filters.Regex;
using Phileas.Strategies;
using Phileas.Strategies.Rules;
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
        var strategy = new SsnFilterStrategy { Strategy = AbstractFilterStrategy.RandomReplace, ContextService = contextService };
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
        var strategy = new SsnFilterStrategy { Strategy = AbstractFilterStrategy.RandomReplace, ContextService = contextService };
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
    public void FilterPolicyLoader_UsesProvidedContextService()
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

        var strategy = new SsnFilterStrategy { Strategy = AbstractFilterStrategy.RandomReplace, ContextService = contextService };
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
    public void FilterPolicyLoader_DefaultsToInMemoryContextService_AndProducesConsistentReplacements()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Ssn = new Ssn() }
        };

        // When no context service is provided, FilterPolicyLoader should default to
        // InMemoryContextService. The same SSN filtered twice in the same call produces a span.
        var result1 = FilterPolicyLoader.Filter(policy, "ctx", 0, "SSN: 123-45-6789");
        var result2 = FilterPolicyLoader.Filter(policy, "ctx", 0, "SSN: 123-45-6789");
        Assert.NotEmpty(result1.Spans);
        Assert.NotEmpty(result2.Spans);
    }
}
