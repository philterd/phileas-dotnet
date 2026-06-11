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

using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Phileas.Services.Anonymization;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;
using PolicySsnStrategy = Phileas.Policy.Filters.Strategies.SsnFilterStrategy;

namespace Phileas.Tests;

public class RandomReplaceAnonymizationTests
{
    private static PhileasPolicy RandomReplaceSsnPolicy()
    {
        return new PhileasPolicy
        {
            Identifiers = new Identifiers
            {
                Ssn = new Ssn
                {
                    Strategies = new List<PolicySsnStrategy>
                        { new() { Strategy = "RANDOM_REPLACE", ReplacementScope = "CONTEXT" } }
                }
            }
        };
    }

    [Fact]
    public void RandomReplace_ProducesRealisticValueNotGuid()
    {
        var result = new FilterService().Filter(RandomReplaceSsnPolicy(), "ctx", 0, "SSN: 123-45-6789");

        Assert.Single(result.Spans);
        var replacement = result.Spans[0].Replacement;

        // The anonymization service replaces the digits and keeps the dashes, so the replacement is a
        // realistic SSN-shaped value of the same length — not a 36-character UUID.
        Assert.Matches(@"^[0-9-]+$", replacement);
        Assert.Equal("123-45-6789".Length, replacement.Length);
        Assert.NotEqual("123-45-6789", replacement);
    }

    [Fact]
    public void RandomReplace_IsReferentiallyConsistentWithinContext()
    {
        var contextService = new InMemoryContextService();
        var service = new FilterService();
        var policy = RandomReplaceSsnPolicy();

        var first = service.Filter(policy, "ctx", 0, "SSN: 123-45-6789", contextService).Spans[0].Replacement;
        var second = service.Filter(policy, "ctx", 0, "SSN: 123-45-6789", contextService).Spans[0].Replacement;

        Assert.Equal(first, second);
    }

    [Fact]
    public void Factory_MapsKnownTypesToDedicatedServices()
    {
        var contextService = new InMemoryContextService();
        Assert.IsType<ZipCodeAnonymizationService>(
            AnonymizationServiceFactory.Create(FilterType.ZipCode, contextService, new Random()));
        Assert.IsType<CreditCardAnonymizationService>(
            AnonymizationServiceFactory.Create(FilterType.CreditCard, contextService, new Random()));
        Assert.IsType<DateAnonymizationService>(
            AnonymizationServiceFactory.Create(FilterType.Date, contextService, new Random()));
    }

    [Fact]
    public void Factory_FallsBackToAlphanumericForUnmappedTypes()
    {
        var service = AnonymizationServiceFactory.Create(FilterType.Ssn, new InMemoryContextService(), new Random());
        Assert.IsType<AlphanumericAnonymizationService>(service);
    }
}
