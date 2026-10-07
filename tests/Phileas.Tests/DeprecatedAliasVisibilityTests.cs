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

using System.Reflection;
using System.Text.Json.Serialization;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Xunit;

namespace Phileas.Tests;

/// <summary>
///     The deprecated <c>person</c> and <c>dictionary</c> keys still load, but are bound through non-public
///     properties, so reflecting over the public properties of <see cref="Identifiers" /> does not find
///     filters that never hold a value. See philterd/phileas-dotnet#158.
/// </summary>
public class DeprecatedAliasVisibilityTests
{
    private static Identifiers Load(string json) => PolicySerializer.DeserializeFromJson(json).Identifiers;

    [Fact]
    public void TheAliasesAreNotPublicProperties()
    {
        var properties = typeof(Identifiers).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.DoesNotContain(properties, p => p.Name is "Person" or "Dictionaries");
        Assert.DoesNotContain(properties, p =>
            p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name is "person" or "dictionary");
    }

    [Fact]
    public void EveryPublicPropertyReturnsWhatWasSet()
    {
        // The aliases were write-only in effect: setting one and reading it back gave null. Anything that
        // discovers filters by reflection takes such a property for a filter that never holds a value.
        foreach (var property in typeof(Identifiers).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite || property.GetIndexParameters().Length > 0) continue;

            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (type.IsValueType || type == typeof(string) || type.IsAbstract) continue;

            var value = Activator.CreateInstance(type);
            var identifiers = new Identifiers();
            property.SetValue(identifiers, value);

            Assert.True(property.GetValue(identifiers) != null, $"{property.Name} returned null after being set");
        }
    }

    [Fact]
    public void TheDictionaryKeyIsFoldedInAfterDeclaredDictionaries()
    {
        var identifiers = Load("{\"identifiers\":{"
                               + "\"dictionaries\":[{\"classification\":\"declared\",\"terms\":[\"alpha\"]}],"
                               + "\"dictionary\":[{\"terms\":[\"beta\"]}]}}");

        Assert.Equal(2, identifiers.CustomDictionaries!.Count);
        Assert.Equal("declared", identifiers.CustomDictionaries[0].Classification);
        Assert.Equal(new[] { "alpha" }, identifiers.CustomDictionaries[0].Terms);
        Assert.Equal(new[] { "beta" }, identifiers.CustomDictionaries[1].Terms);
    }

    [Fact]
    public void BothLegacyKeysRoundTripAsTheirCanonicalKeys()
    {
        const string legacy = "{\"identifiers\":{"
                              + "\"pheyes\":[{\"phEyeConfiguration\":{\"endpoint\":\"http://a:8080\"}}],"
                              + "\"person\":{\"phEyeConfiguration\":{\"endpoint\":\"http://legacy:8080\"}},"
                              + "\"dictionary\":[{\"terms\":[\"beta\"]}]}}";

        var json = PolicySerializer.SerializeToJson(PolicySerializer.DeserializeFromJson(legacy));
        var reloaded = Load(json);

        Assert.DoesNotContain("\"person\"", json);
        Assert.DoesNotContain("\"dictionary\"", json);
        Assert.Equal(new[] { "http://a:8080", "http://legacy:8080" },
            reloaded.PhEyes!.Select(p => p.PhEyeConfiguration!.Endpoint));
        Assert.Equal(new[] { "beta" }, Assert.Single(reloaded.CustomDictionaries!).Terms);
    }
}
