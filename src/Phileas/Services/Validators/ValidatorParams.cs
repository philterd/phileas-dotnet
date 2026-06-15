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

namespace Phileas.Services.Validators;

/// <summary>Helpers for reading the optional params of a parameterized identifier validator.</summary>
internal static class ValidatorParams
{
    /// <summary>Returns the named string parameter, or <see langword="null" /> if absent.</summary>
    public static string? GetString(IReadOnlyDictionary<string, JsonElement>? parameters, string key)
    {
        if (parameters != null && parameters.TryGetValue(key, out var element) &&
            element.ValueKind == JsonValueKind.String)
            return element.GetString();
        return null;
    }

    /// <summary>
    ///     Reads a string-to-string substitution map parameter, with keys and values upper-cased to
    ///     match upper-cased input. Returns <paramref name="defaultValue" /> when absent or not an object.
    /// </summary>
    public static IReadOnlyDictionary<string, string> GetStringMap(
        IReadOnlyDictionary<string, JsonElement>? parameters, string key,
        IReadOnlyDictionary<string, string> defaultValue)
    {
        if (parameters == null || !parameters.TryGetValue(key, out var element) ||
            element.ValueKind != JsonValueKind.Object)
            return defaultValue;

        var result = new Dictionary<string, string>();
        foreach (var property in element.EnumerateObject())
        {
            var value = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.ToString();
            result[property.Name.ToUpperInvariant()] = value.ToUpperInvariant();
        }

        return result;
    }
}
