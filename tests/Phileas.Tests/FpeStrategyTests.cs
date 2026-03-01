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
using Phileas.Filters.Strategies.Rules;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

public class FpeStrategyTests
{
    // 256-bit key (32 bytes) Base64-encoded
    private const string TestKey = "dGVzdGtleXRlc3RrZXl0ZXN0a2V5dGVzdGtleXQ="; // 32 bytes
    private const string TestTweak = "dHdlYWs="; // 5 bytes

    private static SsnFilter CreateSsnFilterWithFpe(string? key = TestKey, string? tweak = null)
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

    private static PhileasPolicy CreateSsnPolicy(Fpe? fpe = null) =>
        new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Ssn = new Ssn() },
            Fpe = fpe ?? new Fpe { Key = TestKey }
        };

    [Fact]
    public void FpeStrategy_WithValidKey_ProducesNonOriginalOutput()
    {
        var filter = CreateSsnFilterWithFpe();
        var policy = CreateSsnPolicy();
        var result = filter.Filter(policy, "ctx", 0, "SSN: 123-45-6789");

        Assert.NotEmpty(result.Spans);
        Assert.NotEqual("123-45-6789", result.Spans[0].Replacement);
    }

    [Fact]
    public void FpeStrategy_PreservesDigitsAsDigits()
    {
        var filter = CreateSsnFilterWithFpe();
        var policy = CreateSsnPolicy();
        var result = filter.Filter(policy, "ctx", 0, "SSN: 123-45-6789");

        Assert.NotEmpty(result.Spans);
        var replacement = result.Spans[0].Replacement;

        // Replacement must have the same format: DDD-DD-DDDD
        Assert.Equal(11, replacement.Length);
        Assert.Equal('-', replacement[3]);
        Assert.Equal('-', replacement[6]);
        Assert.All(replacement.Where((c, i) => i != 3 && i != 6), c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void FpeStrategy_IsDeterministic()
    {
        var filter = CreateSsnFilterWithFpe();
        var policy = CreateSsnPolicy();
        const string input = "SSN: 123-45-6789";

        var result1 = filter.Filter(policy, "ctx", 0, input);
        var result2 = filter.Filter(policy, "ctx", 0, input);

        Assert.NotEmpty(result1.Spans);
        Assert.NotEmpty(result2.Spans);
        Assert.Equal(result1.Spans[0].Replacement, result2.Spans[0].Replacement);
    }

    [Fact]
    public void FpeStrategy_DifferentKeys_ProduceDifferentOutput()
    {
        var key1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"; // 28+ bytes in base64
        var key2 = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

        var filter1 = CreateSsnFilterWithFpe(key1);
        var filter2 = CreateSsnFilterWithFpe(key2);

        var policy1 = new PhileasPolicy { Name = "test", Identifiers = new Identifiers { Ssn = new Ssn() }, Fpe = new Fpe { Key = key1 } };
        var policy2 = new PhileasPolicy { Name = "test", Identifiers = new Identifiers { Ssn = new Ssn() }, Fpe = new Fpe { Key = key2 } };

        var result1 = filter1.Filter(policy1, "ctx", 0, "SSN: 123-45-6789");
        var result2 = filter2.Filter(policy2, "ctx", 0, "SSN: 123-45-6789");

        Assert.NotEmpty(result1.Spans);
        Assert.NotEmpty(result2.Spans);
        Assert.NotEqual(result1.Spans[0].Replacement, result2.Spans[0].Replacement);
    }

    [Fact]
    public void FpeStrategy_WithTweak_IsDeterministic()
    {
        var filter = CreateSsnFilterWithFpe(TestKey, TestTweak);
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Ssn = new Ssn() },
            Fpe = new Fpe { Key = TestKey, Tweak = TestTweak }
        };

        var result1 = filter.Filter(policy, "ctx", 0, "SSN: 234-56-7890");
        var result2 = filter.Filter(policy, "ctx", 0, "SSN: 234-56-7890");

        Assert.NotEmpty(result1.Spans);
        Assert.Equal(result1.Spans[0].Replacement, result2.Spans[0].Replacement);
    }

    [Fact]
    public void FpeStrategy_WithNullFpe_FallsBackToRedact()
    {
        var strategy = new SsnFilterStrategy { Strategy = "FPE_ENCRYPT_REPLACE" };
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { strategy })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        var filter = new SsnFilter(config);
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Ssn = new Ssn() }
            // No Fpe configured
        };

        var result = filter.Filter(policy, "ctx", 0, "SSN: 123-45-6789");

        Assert.NotEmpty(result.Spans);
        Assert.Contains("REDACTED", result.Spans[0].Replacement);
    }

    [Fact]
    public void FpeStrategy_DifferentInputs_ProduceDifferentOutputs()
    {
        var filter = CreateSsnFilterWithFpe();
        var policy = CreateSsnPolicy();

        var result1 = filter.Filter(policy, "ctx", 0, "SSN: 123-45-6789");
        var result2 = filter.Filter(policy, "ctx", 0, "SSN: 456-78-9012");

        Assert.NotEmpty(result1.Spans);
        Assert.NotEmpty(result2.Spans);
        Assert.NotEqual(result1.Spans[0].Replacement, result2.Spans[0].Replacement);
    }
}
