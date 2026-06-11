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
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

public class FpeStrategyTests
{
    // FF3 keys and tweaks are hex-encoded. Key is a 128-bit AES key; tweak is 56- or 64-bit.
    private const string TestKey = "EF4359D8D580AA4F7F036D6F04FC6A94";
    private const string TestTweak = "D8E7920AFA330A73";

    private static SsnFilter CreateSsnFilterWithFpe(string key = TestKey, string tweak = TestTweak)
    {
        var strategy = new SsnFilterStrategy { Strategy = "FPE_ENCRYPT_REPLACE" };
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { strategy })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .WithFpe(new Fpe { Key = key, Tweak = tweak })
            .Build();
        return new SsnFilter(config);
    }

    private static PhileasPolicy SsnPolicy()
    {
        return new PhileasPolicy { Identifiers = new Identifiers { Ssn = new Ssn() } };
    }

    [Fact]
    public void WithValidKey_ProducesNonOriginalOutput()
    {
        var result = CreateSsnFilterWithFpe().Filter(SsnPolicy(), "ctx", 0, "SSN: 123-45-6789");

        Assert.NotEmpty(result.Spans);
        Assert.NotEqual("123-45-6789", result.Spans[0].Replacement);
    }

    [Fact]
    public void PreservesDigitsAsDigits()
    {
        var result = CreateSsnFilterWithFpe().Filter(SsnPolicy(), "ctx", 0, "SSN: 123-45-6789");

        Assert.NotEmpty(result.Spans);
        var replacement = result.Spans[0].Replacement;

        // The structural dashes are preserved and the digits remain digits: DDD-DD-DDDD.
        Assert.Equal(11, replacement.Length);
        Assert.Equal('-', replacement[3]);
        Assert.Equal('-', replacement[6]);
        Assert.All(replacement.Where((_, i) => i != 3 && i != 6), c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void IsDeterministic()
    {
        var filter = CreateSsnFilterWithFpe();
        var policy = SsnPolicy();

        var r1 = filter.Filter(policy, "ctx", 0, "SSN: 123-45-6789");
        var r2 = filter.Filter(policy, "ctx", 0, "SSN: 123-45-6789");

        Assert.Equal(r1.Spans[0].Replacement, r2.Spans[0].Replacement);
    }

    [Fact]
    public void DifferentKeys_ProduceDifferentOutput()
    {
        const string key1 = "00000000000000000000000000000000";
        const string key2 = "11111111111111111111111111111111";

        var r1 = CreateSsnFilterWithFpe(key1).Filter(SsnPolicy(), "ctx", 0, "SSN: 123-45-6789");
        var r2 = CreateSsnFilterWithFpe(key2).Filter(SsnPolicy(), "ctx", 0, "SSN: 123-45-6789");

        Assert.NotEqual(r1.Spans[0].Replacement, r2.Spans[0].Replacement);
    }

    [Fact]
    public void DifferentTweaks_ProduceDifferentOutput()
    {
        var r1 = CreateSsnFilterWithFpe(tweak: "D8E7920AFA330A73").Filter(SsnPolicy(), "ctx", 0, "SSN: 123-45-6789");
        var r2 = CreateSsnFilterWithFpe(tweak: "9A768A92F60E12D8").Filter(SsnPolicy(), "ctx", 0, "SSN: 123-45-6789");

        Assert.NotEqual(r1.Spans[0].Replacement, r2.Spans[0].Replacement);
    }

    [Fact]
    public void DifferentInputs_ProduceDifferentOutputs()
    {
        var filter = CreateSsnFilterWithFpe();
        var policy = SsnPolicy();

        var r1 = filter.Filter(policy, "ctx", 0, "SSN: 123-45-6789");
        var r2 = filter.Filter(policy, "ctx", 0, "SSN: 456-78-9012");

        Assert.NotEqual(r1.Spans[0].Replacement, r2.Spans[0].Replacement);
    }

    [Fact]
    public void MissingFpe_FailsValidation()
    {
        var strategy = new SsnFilterStrategy { Strategy = "FPE_ENCRYPT_REPLACE" };
        var builder = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { strategy })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>());

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("FPE", ex.Message);
    }
}
