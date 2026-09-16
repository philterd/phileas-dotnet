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
using Phileas.Policy;
using Phileas.Services;
using Xunit;

namespace Phileas.Tests;

/// <summary>
///     The optional <c>id</c> label the schema declares on every filter and on both strategy bases, so
///     a component can be named in logs and diagnostics. Neither was bound, so
///     <c>OPTIONS (id = '...')</c> was dropped. See philterd/phileas-dotnet#83.
/// </summary>
public class ComponentIdTests
{
    private const string Policy =
        "{\"identifiers\":{\"ssn\":{\"id\":\"ssn-filter\",\"ssnFilterStrategies\":"
        + "[{\"strategy\":\"MASK\",\"id\":\"ssn-mask\"}]}}}";

    [Fact]
    public void AFilterBindsItsId()
    {
        Assert.Equal("ssn-filter", PolicySerializer.DeserializeFromJson(Policy).Identifiers.Ssn!.Id);
    }

    [Fact]
    public void AStrategyBindsItsId()
    {
        var strategy = Assert.Single(PolicySerializer.DeserializeFromJson(Policy).Identifiers.Ssn!.Strategies!);

        Assert.Equal("ssn-mask", strategy.Id);
    }

    [Fact]
    public void ADateStrategyBindsItsId()
    {
        // The schema declares id on dateFilterStrategy separately from baseFilterStrategy; here the one
        // property on the shared base covers both.
        const string json = "{\"identifiers\":{\"date\":{\"id\":\"date-filter\",\"dateFilterStrategies\":"
                            + "[{\"strategy\":\"SHIFT\",\"shiftDays\":30,\"id\":\"date-shift\"}]}}}";
        var date = PolicySerializer.DeserializeFromJson(json).Identifiers.Date!;

        Assert.Equal("date-filter", date.Id);
        Assert.Equal("date-shift", Assert.Single(date.Strategies!).Id);
    }

    [Fact]
    public void AnIdIsNotWrittenWhenItWasNotSet()
    {
        // Null-omitted, so a policy that never used the label is unchanged by this.
        var round = PolicySerializer.SerializeToJson(
            PolicySerializer.DeserializeFromJson("{\"identifiers\":{\"ssn\":{}}}"));

        Assert.DoesNotContain("\"id\"", round);
    }

    [Fact]
    public void AnIdSurvivesARoundTrip()
    {
        var round = PolicySerializer.SerializeToJson(PolicySerializer.DeserializeFromJson(Policy));

        Assert.Contains("\"id\":\"ssn-filter\"", round);
        Assert.Contains("\"id\":\"ssn-mask\"", round);
    }

    [Fact]
    public void AnIdDoesNotChangeWhatIsRedacted()
    {
        // The label is diagnostic only: the same policy with and without it must filter identically.
        const string withoutIds = "{\"identifiers\":{\"ssn\":{\"ssnFilterStrategies\":[{\"strategy\":\"MASK\"}]}}}";

        Assert.Equal(
            new FilterService().Filter(PolicySerializer.DeserializeFromJson(withoutIds), "ctx", 0, "078-05-1120")
                .FilteredText,
            new FilterService().Filter(PolicySerializer.DeserializeFromJson(Policy), "ctx", 0, "078-05-1120")
                .FilteredText);
    }

    [Fact]
    public void AnIdOnTheDeprecatedDictionaryKeySurvivesTheFold()
    {
        // #88 folds identifiers.dictionary into dictionaries. That conversion listed the shared
        // properties by hand, so a property added to the base later was dropped in the fold.
        const string json = "{\"identifiers\":{\"dictionary\":[{\"id\":\"dict-1\",\"name\":\"n\","
                            + "\"terms\":[\"x\"],\"priority\":3,\"windowSize\":7}]}}";
        var entry = Assert.Single(PolicySerializer.DeserializeFromJson(json).Identifiers.CustomDictionaries!);

        Assert.Equal("dict-1", entry.Id);
        Assert.Equal(3, entry.Priority);
        Assert.Equal(7, entry.WindowSize);
    }

    [Fact]
    public void EveryPropertyOfTheFilterBaseSurvivesTheFold()
    {
        // Drift-proof: a property added to AbstractPolicyFilter has to cross the fold without anyone
        // remembering to add it here.
        var deprecated = new Phileas.Policy.Filters.Dictionary
        {
            Id = "the-id", Enabled = false, Priority = 5, WindowSize = 9,
            Ignored = new List<string> { "a" }, IgnoredFiles = new List<string> { "f" },
            IgnoredPatterns = new List<IgnoredPattern> { new() { Name = "n", Pattern = "^x$" } }
        };

        var policy = PolicySerializer.DeserializeFromJson("{\"identifiers\":{}}");
        policy.Identifiers.Dictionaries = new List<Phileas.Policy.Filters.Dictionary> { deprecated };
        var converted = Assert.Single(policy.Identifiers.CustomDictionaries!);

        foreach (var property in typeof(Phileas.Policy.Filters.AbstractPolicyFilter).GetProperties())
        {
            if (!property.CanRead || !property.CanWrite) continue;

            Assert.Equal(property.GetValue(deprecated), property.GetValue(converted));
        }
    }

    [Fact]
    public void TheSpecExampleRoundTripsWithNoFieldLost()
    {
        // A vendored copy of the specification's own component-ids example, which exists to exercise
        // exactly this field.
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "SpecExamples", "component-ids.json");
        Assert.True(File.Exists(path), $"missing test data: {path}");

        var original = File.ReadAllText(path);
        var round = PolicySerializer.SerializeToJson(PolicySerializer.DeserializeFromJson(original));

        var before = new List<string>();
        var after = new List<string>();
        Leaves(JsonDocument.Parse(original).RootElement, string.Empty, before);
        Leaves(JsonDocument.Parse(round).RootElement, string.Empty, after);

        var lost = before.Where(leaf => !after.Contains(leaf)).ToList();
        Assert.True(lost.Count == 0, "lost on round-trip: " + string.Join(", ", lost));
    }

    [Fact]
    public void TheSpecExampleValidatesAfterARoundTrip()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "SpecExamples", "component-ids.json");
        var round = PolicySerializer.SerializeToJson(
            PolicySerializer.DeserializeFromJson(File.ReadAllText(path)));

        Assert.True(PolicySchema.Validate(round),
            string.Join("; ", PolicySchema.GetValidationErrors(round)));
    }

    private static void Leaves(JsonElement element, string path, List<string> into)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    Leaves(property.Value, path + "/" + property.Name, into);
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                    Leaves(item, path + "/" + index++, into);
                break;
            default:
                into.Add(path + " = " + element);
                break;
        }
    }
}
