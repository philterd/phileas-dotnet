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

using System.Text.Json.Nodes;

namespace Phileas.Policy;

/// <summary>
///     Rewrites the spellings this port accepts into the ones the redaction policy schema declares, so
///     a policy can be validated by what it means rather than by its literal text.
///     <para>
///         This port deliberately accepts more than the schema does: a strategy name in any casing, the
///         older <c>SHIFT_DATE</c> name, and the deprecated <c>identifiers.dictionary</c> key. Each is
///         documented and each keeps an existing policy working. Validating the raw text would report
///         all of them as errors, so validation runs against the normalized form instead. Only the
///         validator sees this; the policy itself is bound from the caller's original JSON.
///     </para>
/// </summary>
internal static class PolicyNormalizer
{
    /// <summary>Returns a normalized copy of <paramref name="policy" />, leaving the original alone.</summary>
    public static JsonNode? Normalize(JsonNode? policy)
    {
        // Deep-copied first: the caller's node is theirs, and the policy is bound from the original.
        var copy = policy?.DeepClone();
        if (copy is not JsonObject root) return copy;

        NormalizeStrategyNames(root);
        DropRemovedIgnoredPatternFields(root);
        FoldDeprecatedDictionaries(root);

        return root;
    }

    /// <summary>
    ///     Upper-cases every strategy name and rewrites <c>SHIFT_DATE</c> to the schema's <c>SHIFT</c>.
    ///     Matching is case-insensitive at runtime and <c>SHIFT_DATE</c> is kept as an alias, neither of
    ///     which the schema's enums allow.
    /// </summary>
    private static void NormalizeStrategyNames(JsonNode node)
    {
        Walk(node, obj =>
        {
            foreach (var name in new[] { "strategy", "fallbackStrategy" })
            {
                if (!obj.TryGetPropertyValue(name, out var value) || value is not JsonValue jsonValue) continue;
                if (!jsonValue.TryGetValue<string>(out var text) || text == null) continue;

                var upper = text.ToUpperInvariant();
                obj[name] = upper == "SHIFT_DATE" ? "SHIFT" : upper;
            }
        });
    }

    /// <summary>
    ///     Drops <c>caseSensitive</c> from every ignored pattern. The schema declares only <c>name</c>
    ///     and <c>pattern</c> there; the field was removed from this port and an existing policy still
    ///     carrying it should load rather than be rejected for a key that no longer does anything.
    /// </summary>
    private static void DropRemovedIgnoredPatternFields(JsonNode node)
    {
        Walk(node, obj =>
        {
            if (!obj.TryGetPropertyValue("ignoredPatterns", out var patterns)) return;
            if (patterns is not JsonArray array) return;

            foreach (var entry in array)
                if (entry is JsonObject pattern)
                    pattern.Remove("caseSensitive");
        });
    }

    /// <summary>
    ///     Moves <c>identifiers.dictionary</c> entries into <c>identifiers.dictionaries</c>, renaming the
    ///     fields as <see cref="Filters.Dictionary.ToCustomDictionary" /> does. Keys the schema does not
    ///     define are carried across untouched rather than dropped, so a typo inside a deprecated entry
    ///     is still reported.
    /// </summary>
    private static void FoldDeprecatedDictionaries(JsonObject root)
    {
        if (root["identifiers"] is not JsonObject identifiers) return;
        if (identifiers["dictionary"] is not JsonArray deprecated) return;

        var canonical = identifiers["dictionaries"] as JsonArray;
        if (canonical == null)
        {
            canonical = new JsonArray();
            identifiers["dictionaries"] = canonical;
        }

        foreach (var entry in deprecated.ToList())
        {
            deprecated.Remove(entry);
            if (entry is not JsonObject dictionary)
            {
                canonical.Add(entry);
                continue;
            }

            Rename(dictionary, "name", "classification");
            Rename(dictionary, "dictionaryFilterStrategies", "customFilterStrategies");

            if (dictionary.TryGetPropertyValue("level", out var level))
            {
                dictionary.Remove("level");
                dictionary["sensitivity"] = ToSensitivity(level?.GetValue<string>());
            }

            canonical.Add(dictionary);
        }

        identifiers.Remove("dictionary");
    }

    /// <summary>
    ///     The <c>level</c> scale counted upward and <c>sensitivity</c> counts downward, so the mapping
    ///     goes by the edit distance each accepts. Mirrors
    ///     <see cref="Filters.Dictionary.ToCustomDictionary" />, which does the same for the bound model.
    /// </summary>
    private static string ToSensitivity(string? level)
    {
        return level?.ToLowerInvariant() switch
        {
            "medium" => "low",
            "high" => "low",
            _ => "medium"
        };
    }

    private static void Rename(JsonObject obj, string from, string to)
    {
        if (!obj.TryGetPropertyValue(from, out var value)) return;

        obj.Remove(from);
        obj[to] = value;
    }

    /// <summary>Applies <paramref name="action" /> to every object in the tree.</summary>
    private static void Walk(JsonNode node, Action<JsonObject> action)
    {
        switch (node)
        {
            case JsonObject obj:
                action(obj);
                foreach (var property in obj.ToList())
                    if (property.Value != null)
                        Walk(property.Value, action);
                break;
            case JsonArray array:
                foreach (var item in array)
                    if (item != null)
                        Walk(item, action);
                break;
        }
    }
}
