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
using Phileas.Filters.Strategies.Rules;
using Xunit;

namespace Phileas.Tests;

/// <summary>
///     Strategy names are matched without regard to case, so a policy written with any casing behaves
///     the same. Before this, a lowercase name missed the switch and silently redacted.
/// </summary>
public class StrategyNameCaseTests
{
    private static readonly string[] NoWindow = [];

    private static string Replace(AbstractFilterStrategy strategy, string token)
    {
        return strategy.GetReplacement("ctx", token, NoWindow, 0.9, null, null, null, null).Value;
    }

    [Theory]
    [InlineData("same")]
    [InlineData("Same")]
    [InlineData("sAmE")]
    public void SameIsRecognisedWhateverItsCasing(string strategy)
    {
        Assert.Equal("123-45-6789", Replace(new SsnFilterStrategy { Strategy = strategy }, "123-45-6789"));
    }

    [Fact]
    public void EveryStandardStrategyNameIsRecognisedInLowercase()
    {
        // REDACT and RANDOM_REPLACE are omitted: redaction is the fallback the switch already lands on,
        // so neither can distinguish a matched name from an unmatched one.
        Assert.Equal("6789", Replace(new SsnFilterStrategy { Strategy = "last_4" }, "123-45-6789"));
        Assert.Equal("1", Replace(new SsnFilterStrategy { Strategy = "truncate" }, "123-45-6789"));
        Assert.Equal("***********", Replace(new SsnFilterStrategy { Strategy = "mask" }, "123-45-6789"));
        Assert.Equal("S", Replace(new SsnFilterStrategy { Strategy = "abbreviate" }, "Smith"));
        Assert.Equal("fixed",
            Replace(new SsnFilterStrategy { Strategy = "static_replace", StaticReplacement = "fixed" },
                "123-45-6789"));
        Assert.Equal(Replace(new SsnFilterStrategy { Strategy = AbstractFilterStrategy.HashSha256Replace },
                "123-45-6789"),
            Replace(new SsnFilterStrategy { Strategy = "hash_sha256_replace" }, "123-45-6789"));
    }

    [Fact]
    public void AMapReplaceFallbackNameIsRecognisedInLowercase()
    {
        // The fallback is a second name read off the policy, so it needs the same leniency as the
        // strategy itself.
        var strategy = new SsnFilterStrategy
        {
            Strategy = "map_replace",
            FallbackStrategy = "same",
            Mappings = new Dictionary<string, string>()
        };
        strategy.InitializeMappings(null);

        Assert.Equal("123-45-6789", Replace(strategy, "123-45-6789"));
    }

    [Fact]
    public void AnUnknownNameStillRedacts()
    {
        // Case-insensitivity is not the same as accepting anything: a name that is not a strategy
        // keeps falling through to redaction, which is the safe outcome on every filter but date.
        Assert.Contains("REDACTED", Replace(new SsnFilterStrategy { Strategy = "NOPE" }, "123-45-6789"));
    }
}
