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

using System.Collections.Concurrent;
using Phileas.Policy;
using Phileas.Services;
using Xunit;

namespace Phileas.Tests;

/// <summary>
///     A policy is validated against the redaction policy schema as it loads, so a key the schema does
///     not define is reported instead of being skipped in silence. See philterd/phileas-dotnet#81.
/// </summary>
public class PolicyValidationOnLoadTests
{
    [Theory]
    [InlineData("{\"identifiers\":{\"notAFilter\":{}}}", "/identifiers/notAFilter")]
    [InlineData("{\"identifiers\":{\"ssn\":{\"onlyValidSSNs\":true}}}", "/identifiers/ssn/onlyValidSSNs")]
    [InlineData("{\"identifiers\":{\"ssn\":{\"ssnFilterStrategies\":[{\"strategy\":\"NOPE\"}]}}}",
        "/identifiers/ssn/ssnFilterStrategies/0/strategy")]
    [InlineData("{\"identifiers\":{\"zipCode\":\"should-be-an-object\"}}", "/identifiers/zipCode")]
    [InlineData("{\"notATopLevelKey\":1}", "/notATopLevelKey")]
    public void AKeyTheSchemaDoesNotDefineIsReported(string json, string location)
    {
        // System.Text.Json skips an unrecognised key, so a misspelled filter used to load as an absent
        // one: the policy was accepted and then quietly did not redact what it named.
        var ex = Assert.Throws<PolicyValidationException>(() => PolicySerializer.DeserializeFromJson(json));

        Assert.Contains(location, string.Join("; ", ex.Errors));
        Assert.Contains(location, ex.Message);
    }

    [Fact]
    public void TheMessageNamesTheSchemaVersionItWasCheckedAgainst()
    {
        var ex = Assert.Throws<PolicyValidationException>(
            () => PolicySerializer.DeserializeFromJson("{\"identifiers\":{\"notAFilter\":{}}}"));

        Assert.Contains(PolicySchema.GetSupportedSchemaVersion(), ex.Message);
    }

    [Fact]
    public void ValidationIsOptOut()
    {
        // For a policy written against a different schema version. Whatever the schema would have
        // rejected is skipped rather than applied, which is the cost of opting out.
        const string json = "{\"identifiers\":{\"notAFilter\":{},\"ssn\":{}}}";

        var policy = PolicySerializer.DeserializeFromJson(json, validate: false);

        Assert.NotNull(policy.Identifiers.Ssn);
    }

    [Fact]
    public void AMalformedPolicyIsReportedAsAPolicyErrorNotAValidatorFailure()
    {
        var ex = Assert.Throws<PolicyValidationException>(
            () => PolicySerializer.DeserializeFromJson("{not json"));

        Assert.Contains("well-formed JSON", ex.Message);
    }

    // ---------------- the spellings this port accepts are not schema errors ----------------

    [Theory]
    [InlineData("{\"identifiers\":{\"date\":{\"dateFilterStrategies\":[{\"strategy\":\"shift\"}]}}}")]
    [InlineData("{\"identifiers\":{\"date\":{\"dateFilterStrategies\":[{\"strategy\":\"Relative\"}]}}}")]
    [InlineData("{\"identifiers\":{\"date\":{\"dateFilterStrategies\":[{\"strategy\":\"SHIFT_DATE\"}]}}}")]
    [InlineData("{\"identifiers\":{\"ssn\":{\"ssnFilterStrategies\":[{\"strategy\":\"map_replace\","
                + "\"fallbackStrategy\":\"redact\"}]}}}")]
    [InlineData("{\"identifiers\":{\"dictionary\":[{\"name\":\"n\",\"terms\":[\"x\"],\"level\":\"high\"}]}}")]
    [InlineData("{\"ignoredPatterns\":[{\"name\":\"n\",\"pattern\":\"^x$\",\"caseSensitive\":true}]}")]
    public void ASpellingThisPortAcceptsIsNotReported(string json)
    {
        // Validation is against what the policy means to this port, not its literal text: a strategy
        // name in any casing, the older SHIFT_DATE name, and the deprecated dictionary key all load,
        // as they did before validation existed.
        PolicySerializer.DeserializeFromJson(json);
    }

    [Fact]
    public void TheNormalizedFormIsOnlyForValidation()
    {
        // Normalizing must not change what is bound: the deprecated key still folds through the model,
        // and the policy still redacts.
        const string json = "{\"identifiers\":{\"dictionary\":[{\"name\":\"cond\",\"terms\":[\"diabetes\"]}]}}";

        var filtered = new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, "has diabetes").FilteredText;

        Assert.DoesNotContain("diabetes", filtered);
    }

    [Fact]
    public void AnErrorInsideADeprecatedDictionaryEntryIsStillReported()
    {
        // Folding the deprecated key must not become a way to smuggle an unknown key past validation.
        const string json = "{\"identifiers\":{\"dictionary\":[{\"name\":\"n\",\"terms\":[\"x\"],"
                            + "\"notAKey\":true}]}}";

        var ex = Assert.Throws<PolicyValidationException>(() => PolicySerializer.DeserializeFromJson(json));

        Assert.Contains("notAKey", string.Join("; ", ex.Errors));
    }

    // ---------------- concurrency ----------------

    [Fact]
    public void ValidationIsCorrectWhenRunFromManyThreads()
    {
        // The compiled schema is shared, and a JsonSchema is not safe to evaluate from several threads
        // at once: unguarded, roughly one concurrent evaluation in seven reported an invalid policy as
        // valid. A false "valid" is the one answer validation must never give.
        const string invalid = "{\"identifiers\":{\"notAFilter\":{}}}";
        const string valid = "{\"identifiers\":{\"ssn\":{}}}";
        var wrong = new ConcurrentBag<string>();

        Parallel.For(0, 2_000, new ParallelOptions { MaxDegreeOfParallelism = 16 }, _ =>
        {
            if (PolicySchema.Validate(invalid)) wrong.Add("an invalid policy was reported valid");
            if (!PolicySchema.Validate(valid)) wrong.Add("a valid policy was reported invalid");
        });

        Assert.True(wrong.IsEmpty, string.Join("; ", wrong.Distinct()));
    }
}
