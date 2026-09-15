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
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
///     <c>enabled: false</c> switches a filter off. It was bound and never read, so a policy that
///     turned a filter off still redacted with it. See philterd/phileas-dotnet#123.
/// </summary>
public class FilterEnabledTests
{
    private static string Filter(string json, string input)
    {
        return new FilterService().Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, input)
            .FilteredText;
    }

    [Theory]
    [InlineData("{\"identifiers\":{\"ssn\":{\"enabled\":false}}}", "078-05-1120")]
    [InlineData("{\"identifiers\":{\"emailAddress\":{\"enabled\":false}}}", "someone@example.com")]
    [InlineData("{\"identifiers\":{\"date\":{\"enabled\":false}}}", "01/15/1990")]
    [InlineData("{\"identifiers\":{\"creditCard\":{\"enabled\":false}}}", "4111111111111111")]
    [InlineData("{\"identifiers\":{\"surname\":{\"enabled\":false}}}", "Smith")]
    [InlineData("{\"identifiers\":{\"city\":{\"enabled\":false}}}", "Boston")]
    [InlineData("{\"identifiers\":{\"dictionaries\":[{\"terms\":[\"Acme\"],\"enabled\":false}]}}", "Acme")]
    [InlineData("{\"identifiers\":{\"identifiers\":[{\"classification\":\"c\",\"pattern\":\"XY-[0-9]+\","
                + "\"enabled\":false}]}}", "XY-42")]
    public void ADisabledFilterDetectsNothing(string json, string input)
    {
        Assert.Equal(input, Filter(json, input));
    }

    [Theory]
    [InlineData("{\"identifiers\":{\"ssn\":{\"enabled\":true}}}")]
    [InlineData("{\"identifiers\":{\"ssn\":{}}}")] // the default is enabled
    public void AnEnabledFilterStillDetects(string json)
    {
        Assert.NotEqual("078-05-1120", Filter(json, "078-05-1120"));
    }

    [Fact]
    public void DisablingOneFilterLeavesTheOthersAlone()
    {
        const string json = "{\"identifiers\":{\"ssn\":{\"enabled\":false},\"emailAddress\":{}}}";

        var filtered = Filter(json, "078-05-1120 someone@example.com");

        Assert.Contains("078-05-1120", filtered);
        Assert.DoesNotContain("someone@example.com", filtered);
    }

    [Fact]
    public void EachEntryOfAListCarriesItsOwnSetting()
    {
        const string json = "{\"identifiers\":{\"dictionaries\":["
                            + "{\"terms\":[\"alpha\"],\"enabled\":false},"
                            + "{\"terms\":[\"beta\"],\"enabled\":true}]}}";

        var filtered = Filter(json, "alpha and beta");

        Assert.Contains("alpha", filtered);
        Assert.DoesNotContain("beta", filtered);
    }

    // ---------------- every filter type, without listing them ----------------

    [Fact]
    public void EveryFilterTypeHonoursTheSetting()
    {
        // Driven off the model so a filter added later is covered without anyone remembering to add
        // it here, which is how this setting went unread in the first place.
        var enabled = PopulateEveryFilter(true);
        var disabled = PopulateEveryFilter(false);

        var active = Enum.GetValues<FilterType>().Where(enabled.HasFilter).ToList();
        Assert.True(active.Count > 25, $"only {active.Count} filter types were populated");

        var stillActive = active.Where(disabled.HasFilter).ToList();
        Assert.True(stillActive.Count == 0,
            "these ignore enabled:false: " + string.Join(", ", stillActive));
    }

    private static Identifiers PopulateEveryFilter(bool enabled)
    {
        var identifiers = new Identifiers();

        foreach (var property in typeof(Identifiers).GetProperties())
        {
            if (!property.CanWrite || property.GetIndexParameters().Length > 0) continue;

            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (type.IsValueType || type == typeof(string)) continue;

            var value = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)
                ? MakeSingletonList(type, enabled)
                : MakeFilter(type, enabled);

            if (value != null) property.SetValue(identifiers, value);
        }

        return identifiers;
    }

    private static object? MakeSingletonList(Type listType, bool enabled)
    {
        var element = MakeFilter(listType.GetGenericArguments()[0], enabled);
        if (element == null) return null;

        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
        list.Add(element);
        return list;
    }

    private static object? MakeFilter(Type type, bool enabled)
    {
        if (type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null) return null;
        if (!typeof(AbstractPolicyFilter).IsAssignableFrom(type)) return null;

        var filter = (AbstractPolicyFilter)Activator.CreateInstance(type)!;
        filter.Enabled = enabled;
        return filter;
    }

    [Fact]
    public void ADisabledFilterIsNotBuiltAtAll()
    {
        // Not built and then discarded: a policy whose every filter is off produces no spans, and the
        // document comes back exactly as it went in.
        var identifiers = PopulateEveryFilter(false);
        const string input = "078-05-1120 someone@example.com 4111111111111111 on 01/15/1990 in Boston";

        var result = new FilterService().Filter(new PhileasPolicy { Identifiers = identifiers }, "ctx", 0, input);

        Assert.Empty(result.Spans);
        Assert.Equal(input, result.FilteredText);
    }
}
