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
using Phileas.Services.Generators;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

public class PipelineReplacementValidatorTests
{
    private static PipelineReplacementValidator CreateValidator()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new SsnFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        var filters = new List<AbstractFilter> { new SsnFilter(config) };
        var policy = new PhileasPolicy { Name = "test", Identifiers = new Identifiers { Ssn = new Ssn() } };
        return new PipelineReplacementValidator(policy, filters);
    }

    [Fact]
    public void ContainsPii_DetectsPiiInCandidate()
    {
        Assert.True(CreateValidator().ContainsPii("Their number is 123-45-6789."));
    }

    [Fact]
    public void ContainsPii_ReturnsFalseForBenignCandidate()
    {
        Assert.False(CreateValidator().ContainsPii("Just a friendly greeting."));
    }

    [Fact]
    public void ContainsPii_WhileRescanning_ReturnsFalse()
    {
        // The re-scan guard: a nested call reports no PII so the pipeline can never recurse into itself.
        AbstractFilterStrategy.SetRescanning(true);
        try
        {
            Assert.False(CreateValidator().ContainsPii("123-45-6789"));
        }
        finally
        {
            AbstractFilterStrategy.SetRescanning(false);
        }
    }
}
