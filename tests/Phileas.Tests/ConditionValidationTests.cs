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

using System.Globalization;
using Phileas.Filters.Conditions;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Philterd.PhiSql;
using Xunit;
using PolicySsnStrategy = Phileas.Policy.Filters.Strategies.SsnFilterStrategy;

namespace Phileas.Tests;

/// <summary>
///     A strategy condition that does not parse is rejected when the policy is loaded, and is never treated as
///     satisfied. See philterd/phileas-dotnet#143.
/// </summary>
public class ConditionValidationTests
{
    private static string SsnPolicy(string condition, string id = "") =>
        "{\"identifiers\": {\"ssn\": {\"ssnFilterStrategies\": [" +
        "{" + (id == "" ? "" : "\"id\": \"" + id + "\", ") + "\"strategy\": \"LAST_4\", \"condition\": " +
        System.Text.Json.JsonSerializer.Serialize(condition) + "}, {\"strategy\": \"REDACT\"}]}}}";

    private static PolicyValidationException Rejected(string condition, string id = "") =>
        Assert.Throws<PolicyValidationException>(() => PolicySerializer.DeserializeFromJson(SsnPolicy(condition, id)));

    // The conditions #143 lists, each written in good faith. None is supported yet, so each is rejected.
    [Theory]
    [InlineData("confidence > 0.9 && context == \"medical\"", "'&&'")]
    [InlineData("confidence = 0.9", "'='")]
    [InlineData("confidence > 0.5 or confidence < 0.1", "'or'")]
    [InlineData("token is birthdate", "birthdate")]
    public void TheConditionsInTheIssueAreRejectedAtLoad(string condition, string reasonMentions)
    {
        var e = Rejected(condition);
        var error = Assert.Single(e.Errors);
        Assert.StartsWith("/identifiers/ssn/ssnFilterStrategies/0: the condition ", error);
        Assert.Contains(reasonMentions, error);
    }

    // The other ways a condition used to count as satisfied without being understood.
    [Theory]
    [InlineData("this is not a condition")]
    [InlineData("confidence startswith 5")]
    [InlineData("confidence > \"high\"")]
    [InlineData("population == \"many\"")]
    [InlineData("type > \"PER\"")]
    [InlineData("name == \"x\"")] // unknown field
    [InlineData("token == \"x\" and")]
    [InlineData("(confidence > 0.5)")]
    [InlineData("confidence > 0.5 || confidence < 0.1")]
    [InlineData("token == \"unterminated")]
    [InlineData("confidence > -1")]
    [InlineData("token ==")]
    public void OtherUnparseableConditionsAreRejectedAtLoad(string condition)
    {
        Assert.Single(Rejected(condition).Errors);
    }

    [Theory]
    [InlineData("token == \"x\"")]
    [InlineData("token startswith \"5\"")]
    [InlineData("token is not \"x\"")]
    [InlineData("token is   not \"x\"")]
    [InlineData("context != \"medical\"")]
    [InlineData("confidence >= 0.9")]
    [InlineData("confidence>0.9")]
    [InlineData("Confidence > 0.9 AND token == \"x\"")]
    [InlineData("population < 4500")]
    [InlineData("type == \"PER\"")]
    [InlineData("token == 123")]
    [InlineData("confidence > 0.5 and confidence < 0.9 and context == \"x\"")]
    [InlineData("  ")]
    public void ConditionsInTheGrammarStillLoad(string condition)
    {
        PolicySerializer.DeserializeFromJson(SsnPolicy(condition));
    }

    [Fact]
    public void TheErrorNamesTheStrategyByIdWithoutPrintingTheCondition()
    {
        // A condition can carry a value from the data being redacted; it must not reach a log via the message.
        var e = Rejected("token == \"123-45-6789\" or token == \"987-65-4321\"", "ssn-last4");

        Assert.Equal("/identifiers/ssn/ssnFilterStrategies/0 (id \"ssn-last4\"): the condition joins comparisons "
                     + "with 'or', which is not supported; only 'and' is", Assert.Single(e.Errors));
        Assert.DoesNotContain("123-45-6789", e.Message);
        Assert.DoesNotContain("987-65-4321", e.Message);
        Assert.StartsWith("The policy has strategy conditions that do not parse", e.Message);
    }

    [Fact]
    public void EveryBadConditionInThePolicyIsReported()
    {
        const string json = "{\"identifiers\": {" +
                            "\"ssn\": {\"ssnFilterStrategies\": [{\"strategy\": \"REDACT\", \"condition\": \"x\"}]}," +
                            "\"pheyes\": [{\"phEyeFilterStrategies\": [{\"strategy\": \"MASK\"}, {\"strategy\": \"REDACT\", \"condition\": \"y\"}]}]}}";
        var e = Assert.Throws<PolicyValidationException>(() => PolicySerializer.DeserializeFromJson(json));

        Assert.Equal(2, e.Errors.Count);
        Assert.Contains(e.Errors, error => error.StartsWith("/identifiers/ssn/ssnFilterStrategies/0:"));
        Assert.Contains(e.Errors, error => error.StartsWith("/identifiers/pheyes/0/phEyeFilterStrategies/1:"));
    }

    [Theory]
    [InlineData("\"person\": {\"phEyeFilterStrategies\": [{\"strategy\": \"REDACT\", \"condition\": \"x\"}]}",
        "/identifiers/pheyes/0/phEyeFilterStrategies/0:")]
    [InlineData("\"zipCode\": {\"zipCodeFilterStrategy\": [{\"strategy\": \"REDACT\", \"condition\": \"x\"}]}",
        "/identifiers/zipCode/zipCodeFilterStrategies/0:")]
    public void AStrategyUnderADeprecatedKeyIsCheckedAndReportedUnderItsCurrentKey(string identifier, string path)
    {
        var e = Assert.Throws<PolicyValidationException>(() =>
            PolicySerializer.DeserializeFromJson("{\"identifiers\": {" + identifier + "}}"));
        Assert.StartsWith(path, Assert.Single(e.Errors));
    }

    [Fact]
    public void ParsingStillWorksAfterManyDistinctConditions()
    {
        // The parse cache is bounded; filling it past the bound must not change any result.
        for (var i = 0; i < 5000; i++)
            Assert.Null(ConditionParser.GetError($"confidence > 0.{i}"));
        Assert.NotNull(ConditionParser.GetError("confidence = 0.5"));
        Assert.True(ConditionEvaluator.Evaluate("confidence > 0.5", "c", "t", 0.9, null));
    }

    [Fact]
    public void ConditionsAreCheckedEvenWithoutSchemaValidation()
    {
        Assert.Throws<PolicyValidationException>(() =>
            PolicySerializer.DeserializeFromJson(SsnPolicy("confidence = 0.9"), validate: false));
    }

    [Fact]
    public void APolicyBuiltInCodeIsRejectedBeforeAnyTextIsFiltered()
    {
        var policy = new Phileas.Policy.Policy
        {
            Identifiers = new Identifiers
            {
                Ssn = new Ssn
                {
                    Strategies = new List<PolicySsnStrategy>
                        { new() { Strategy = "LAST_4", Condition = "confidence > 0.99 or confidence < 0.01" } }
                }
            }
        };

        var e = Assert.Throws<PolicyValidationException>(() =>
            new FilterService().Filter(policy, "ctx", 0, "number 123-45-6789"));
        Assert.StartsWith("/identifiers/ssn/ssnFilterStrategies/0:", Assert.Single(e.Errors));
    }

    [Fact]
    public void APhiSqlOrConditionNoLongerLeaksDigits()
    {
        // The case that made #143 a release blocker: PhiSQL compiles OR, and this port applied LAST_4 to an SSN
        // neither comparison matched, leaving four digits where the author asked for REDACT.
        var json = new Compiler().Compile(
            "POLICY p;\nREDACT SSN WITH LAST_4 WHERE CONFIDENCE > 0.99 OR CONFIDENCE < 0.01;\nREDACT SSN WITH REDACT;")
            .ToJsonString();

        Assert.Throws<PolicyValidationException>(() => PolicySerializer.DeserializeFromJson(json));
    }

    [Fact]
    public void NumbersAreReadTheSameInEveryCulture()
    {
        // double.TryParse used the current culture, so under de-DE "0.9" read as 9.
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.True(ConditionEvaluator.Evaluate("confidence > 0.9", "ctx", "t", 0.95, null));
            Assert.False(ConditionEvaluator.Evaluate("confidence > 0.9", "ctx", "t", 0.85, null));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ParsedConditionsEvaluateAsBefore()
    {
        Assert.True(ConditionEvaluator.Evaluate("token is not \"a\" and confidence <= 0.9", "c", "b", 0.9, null));
        Assert.False(ConditionEvaluator.Evaluate("token is not \"b\" and confidence <= 0.9", "c", "b", 0.9, null));
        Assert.True(ConditionEvaluator.Evaluate("type != \"PER\"", "c", "t", 0.9, null)); // no classification
        Assert.False(ConditionEvaluator.Evaluate("type == \"PER\"", "c", "t", 0.9, null));
        Assert.True(ConditionEvaluator.Evaluate("type is \"per\"", "c", "t", 0.9, "PER"));
        Assert.True(ConditionEvaluator.Evaluate("token startswith \"AB\"", "c", "abc", 0.9, null));
        Assert.True(ConditionEvaluator.Evaluate("token == 123", "c", "123", 0.9, null));
        Assert.True(ConditionEvaluator.Evaluate("context > \"a\"", "b", "t", 0.9, null));
    }
}
