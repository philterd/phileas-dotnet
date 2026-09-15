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

using System.Text.Json;
using Json.Schema;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
///     A policy written by this port validates against the redaction policy schema, which is
///     <c>additionalProperties: false</c> throughout. See philterd/phileas-dotnet#88.
/// </summary>
public class SchemaValidationTests
{
    private static readonly JsonSchema Schema = JsonSchema.FromText(PolicySchema.GetSchema());

    private static IReadOnlyList<string> Validate(string policyJson)
    {
        var result = Schema.Evaluate(JsonDocument.Parse(policyJson).RootElement,
            new EvaluationOptions { OutputFormat = OutputFormat.List });

        if (result.IsValid) return Array.Empty<string>();

        return result.Details
            .Where(detail => detail.HasErrors)
            .SelectMany(detail => detail.Errors!.Select(e => $"{detail.InstanceLocation}: {e.Value}"))
            .ToList();
    }

    private static string Round(string policyJson)
    {
        return PolicySerializer.SerializeToJson(PolicySerializer.DeserializeFromJson(policyJson));
    }

    [Fact]
    public void AnEmptyPolicySerializesToSomethingTheSchemaAccepts()
    {
        var errors = Validate(PolicySerializer.SerializeToJson(new PhileasPolicy()));
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Theory]
    // The deprecated .NET-only key: the schema declares only "dictionaries".
    [InlineData("{\"identifiers\":{\"dictionary\":[{\"name\":\"n\",\"terms\":[\"x\"],\"level\":\"medium\"}]}}")]
    // The .NET-only ignoredPattern field: the schema declares only name and pattern.
    [InlineData("{\"ignoredPatterns\":[{\"name\":\"n\",\"pattern\":\"^x$\"}]}")]
    [InlineData("{\"identifiers\":{\"ssn\":{\"ignoredPatterns\":[{\"name\":\"n\",\"pattern\":\"^x$\"}]}}}")]
    [InlineData("{\"identifiers\":{\"dictionaries\":[{\"classification\":\"c\",\"terms\":[\"x\"],\"sensitivity\":\"low\"}]}}")]
    [InlineData("{\"identifiers\":{\"date\":{\"dateFilterStrategies\":[{\"strategy\":\"SHIFT\",\"shiftDays\":30}]}}}")]
    [InlineData("{\"identifiers\":{\"creditCard\":{\"onlyValidCreditCardNumbers\":false}}}")]
    public void WhatThisPortWritesValidatesAgainstTheSchema(string policyJson)
    {
        var errors = Validate(Round(policyJson));
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Theory]
    [InlineData("12-date-shift.json")]
    [InlineData("26-filter-options.json")]
    [InlineData("27-strategy-params.json")]
    [InlineData("28-nested-options.json")]
    public void TheSpecExamplesStillValidateAfterARoundTrip(string file)
    {
        // Vendored copies of the PhiSQL specification's own examples. They validate as written; the
        // point here is that what this port writes back does too.
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "SpecExamples", file);
        Assert.True(File.Exists(path), $"missing test data: {path}");

        var errors = Validate(Round(File.ReadAllText(path)));
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void APolicyWithEveryIdentifierPopulatedValidates()
    {
        // Built by reflection so a filter added to the model is covered without anyone remembering to
        // add it here, which is how the gaps this issue is about went unnoticed.
        var identifiers = new Identifiers();
        var populated = 0;

        foreach (var property in typeof(Identifiers).GetProperties())
        {
            if (!property.CanWrite || property.GetIndexParameters().Length > 0) continue;

            var type = property.PropertyType;
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying.IsValueType || underlying == typeof(string)) continue;

            // A list property takes one default element; a filter property takes an instance.
            var instance = underlying.IsGenericType
                           && underlying.GetGenericTypeDefinition() == typeof(List<>)
                ? MakeSingletonList(underlying)
                : TryCreate(underlying);

            if (instance == null) continue;
            property.SetValue(identifiers, instance);
            populated++;
        }

        Assert.True(populated > 25, $"only {populated} identifier properties were populated");

        var errors = Validate(PolicySerializer.SerializeToJson(new PhileasPolicy { Identifiers = identifiers }));
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    private static object? MakeSingletonList(Type listType)
    {
        var element = TryCreate(listType.GetGenericArguments()[0]);
        if (element == null) return null;

        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
        list.Add(element);
        return list;
    }

    private static object? TryCreate(Type type)
    {
        if (type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null) return null;
        try
        {
            return Activator.CreateInstance(type);
        }
        catch
        {
            return null;
        }
    }

    [Fact]
    public void TheDeprecatedDictionaryKeyIsNeverWrittenBack()
    {
        var round = Round("{\"identifiers\":{\"dictionary\":[{\"name\":\"n\",\"terms\":[\"x\"]}]}}");

        Assert.DoesNotContain("\"dictionary\"", round);
        Assert.Contains("\"dictionaries\"", round);
    }

    [Fact]
    public void TheDeprecatedIgnoredPatternFieldIsNeverWrittenBack()
    {
        var round = Round("{\"ignoredPatterns\":[{\"name\":\"n\",\"pattern\":\"^x$\",\"caseSensitive\":true}]}");

        Assert.DoesNotContain("caseSensitive", round);
        Assert.Contains("\"pattern\"", round);
    }

    // ---------------- the deprecated key keeps working ----------------

    [Theory]
    [InlineData("patient has diabetes", true)]
    [InlineData("treated for ASTHMA", true)]
    [InlineData("nothing relevant here", false)]
    public void TheDeprecatedDictionaryKeyStillDetects(string input, bool detected)
    {
        // Folded rather than dropped: a policy using the old key keeps redacting, which is the one
        // outcome that must not change silently.
        const string json = "{\"identifiers\":{\"dictionary\":[{\"name\":\"cond\","
                            + "\"terms\":[\"diabetes\",\"asthma\"]}]}}";
        var filtered = new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, input).FilteredText;

        Assert.Equal(detected, filtered != input);
    }

    [Fact]
    public void BothSpellingsInOnePolicyAreKept()
    {
        const string json = "{\"identifiers\":{\"dictionary\":[{\"name\":\"a\",\"terms\":[\"alpha\"]}],"
                            + "\"dictionaries\":[{\"classification\":\"b\",\"terms\":[\"beta\"]}]}}";
        var policy = PolicySerializer.DeserializeFromJson(json);

        Assert.Equal(2, policy.Identifiers.CustomDictionaries!.Count);

        var filtered = new FilterService().Filter(policy, "ctx", 0, "alpha and beta").FilteredText;
        Assert.DoesNotContain("alpha", filtered);
        Assert.DoesNotContain("beta", filtered);
    }

    [Fact]
    public void TheDeprecatedFieldsAreCarriedOntoTheCanonicalShape()
    {
        const string json = "{\"identifiers\":{\"dictionary\":[{\"name\":\"cond\",\"terms\":[\"x\"],"
                            + "\"fuzzy\":true,\"level\":\"medium\",\"priority\":7,\"windowSize\":9,"
                            + "\"dictionaryFilterStrategies\":[{\"strategy\":\"STATIC_REPLACE\","
                            + "\"staticReplacement\":\"gone\"}]}]}}";
        var dictionary = Assert.Single(PolicySerializer.DeserializeFromJson(json)
            .Identifiers.CustomDictionaries!);

        Assert.Equal("cond", dictionary.Classification); // name -> classification
        Assert.True(dictionary.Fuzzy);
        Assert.Equal(7, dictionary.Priority);
        Assert.Equal(9, dictionary.WindowSize);

        var strategy = Assert.Single(dictionary.Strategies!);
        Assert.Equal("STATIC_REPLACE", strategy.Strategy);
        Assert.Equal("gone", strategy.StaticReplacement);
    }

    [Theory]
    // level counted upward (low=1 edit, medium=2, high=3); sensitivity counts downward (high=0,
    // medium=1, low=2). Mapping the words onto each other would invert how strict the filter is, so
    // the mapping goes by the edit distance each accepts.
    [InlineData("low", "medium")]
    [InlineData("medium", "low")]
    [InlineData("high", "low")] // nothing accepts 3 edits; the loosest available is used
    [InlineData(null, "medium")] // the level default is low
    public void LevelMapsToTheSensitivityThatAcceptsTheSameEditDistance(string? level, string sensitivity)
    {
        var levelJson = level == null ? "" : ",\"level\":\"" + level + "\"";
        var json = "{\"identifiers\":{\"dictionary\":[{\"name\":\"n\",\"terms\":[\"x\"]" + levelJson + "}]}}";

        Assert.Equal(sensitivity,
            Assert.Single(PolicySerializer.DeserializeFromJson(json).Identifiers.CustomDictionaries!).Sensitivity);
    }

    [Theory]
    [InlineData("Smith", true, true)] // exact
    [InlineData("Smiths", true, true)] // one edit
    [InlineData("Smyths", false, true)] // two edits
    [InlineData("Smythes", false, false)] // three: level high used to reach this, nothing does now
    public void TheFoldedFuzzyMatchingAcceptsTheSameDistancesAsBefore(string input, bool atLow, bool atMedium)
    {
        Assert.Equal(atLow, Detects("low", input));
        Assert.Equal(atMedium, Detects("medium", input));
    }

    private static bool Detects(string level, string input)
    {
        var json = "{\"identifiers\":{\"dictionary\":[{\"name\":\"n\",\"terms\":[\"Smith\"],"
                   + "\"fuzzy\":true,\"level\":\"" + level + "\"}]}}";
        return new FilterService().Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, input)
            .FilteredText != input;
    }

    // ---------------- ignoredPattern case sensitivity ----------------

    [Theory]
    [InlineData("^Bob@Example\\.com$", true)] // same case
    [InlineData("^BOB@EXAMPLE\\.COM$", false)] // used to suppress; matching now follows Java
    [InlineData("(?i)^BOB@EXAMPLE\\.COM$", true)] // the opt-in that replaces caseSensitive
    public void AnIgnoredPatternIsCaseSensitive(string pattern, bool suppressed)
    {
        const string input = "Bob@Example.com";
        var json = "{\"identifiers\":{\"emailAddress\":{\"ignoredPatterns\":[{\"name\":\"n\",\"pattern\":\""
                   + pattern.Replace("\\", "\\\\") + "\"}]}}}";
        var filtered = new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, input).FilteredText;

        Assert.Equal(suppressed, filtered == input);
    }

    [Fact]
    public void AnUnknownIgnoredPatternFieldNoLongerBindsAnything()
    {
        // caseSensitive is accepted by the deserializer (unknown keys are skipped) but has no effect,
        // where it used to turn matching case-insensitive.
        const string json = "{\"identifiers\":{\"emailAddress\":{\"ignoredPatterns\":"
                            + "[{\"name\":\"n\",\"pattern\":\"^BOB@EXAMPLE\\\\.COM$\",\"caseSensitive\":false}]}}}";
        var filtered = new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, "Bob@Example.com").FilteredText;

        Assert.NotEqual("Bob@Example.com", filtered);
    }
}
