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
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Phileas.Policy;

/// <summary>
///     Resolves whole-value <c>${NAME}</c> placeholders to environment variables when deserializing
///     policy strings, so secrets and machine-specific values can stay out of the policy document.
///     A placeholder with no matching environment variable is left as-is. Mirrors the Java
///     <c>PlaceholderDeserializer</c>.
/// </summary>
public class PlaceholderStringConverter : JsonConverter<string>
{
    private static readonly Regex Placeholder = new(@"^\$\{([A-Z0-9_]+)\}$", RegexOptions.IgnoreCase);

    /// <inheritdoc />
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString()?.Trim();
        if (value == null) return null;

        var match = Placeholder.Match(value);
        if (!match.Success) return value;

        return Environment.GetEnvironmentVariable(match.Groups[1].Value) ?? value;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
