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

namespace Phileas.Policy.Filters;

/// <summary>
///     Optional post-match validation for the custom <c>identifier</c> filter. A regex match is kept
///     only if the named validator passes, so a generic identifier can reject format-valid but
///     checksum-invalid values. The redaction policy schema allows this to be written as a bare
///     string (the validator name) or as an object with a <c>name</c> and validator-specific
///     <c>params</c>; both deserialize to this class via <see cref="ValidatorJsonConverter" />.
///     Mirrors the Java <c>Validator</c>.
/// </summary>
public class Validator
{
    /// <summary>Initializes a new instance.</summary>
    public Validator()
    {
    }

    /// <summary>Initializes a new instance with a name and optional params.</summary>
    public Validator(string? name, IReadOnlyDictionary<string, JsonElement>? @params = null)
    {
        Name = name;
        Params = @params;
    }

    /// <summary>Gets or sets the validator name (a built-in validator from the catalog).</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the optional validator-specific parameters.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Params { get; set; }
}

/// <summary>
///     Deserializes the schema's <c>oneOf</c> (a string, or a <c>{name, params}</c> object) into a
///     <see cref="Validator" />, and serializes back to the compact string form when there are no
///     params.
/// </summary>
public class ValidatorJsonConverter : JsonConverter<Validator?>
{
    /// <inheritdoc />
    public override Validator? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.String:
                // Compact form: "validator": "luhn"
                return new Validator(reader.GetString());

            case JsonTokenType.StartObject:
                // Object form: "validator": { "name": "luhn", "params": { ... } }
                using (var document = JsonDocument.ParseValue(ref reader))
                {
                    var root = document.RootElement;
                    if (!root.TryGetProperty("name", out var nameElement))
                        throw new JsonException("An identifier validator object must have a 'name'.");

                    var name = nameElement.GetString();

                    IReadOnlyDictionary<string, JsonElement>? @params = null;
                    if (root.TryGetProperty("params", out var paramsElement) &&
                        paramsElement.ValueKind == JsonValueKind.Object)
                        @params = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                            paramsElement.GetRawText(), options);

                    return new Validator(name, @params);
                }

            default:
                throw new JsonException("An identifier validator must be a string or an object with a 'name'.");
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Validator? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value.Params == null || value.Params.Count == 0)
        {
            writer.WriteStringValue(value.Name);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        writer.WritePropertyName("params");
        JsonSerializer.Serialize(writer, value.Params, options);
        writer.WriteEndObject();
    }
}
