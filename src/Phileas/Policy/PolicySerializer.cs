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
using System.Text;
using System.Text.Json;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Phileas.Policy;

/// <summary>
/// Provides methods to serialize and deserialize a <see cref="Policy"/> to and from
/// JSON or YAML format.
/// </summary>
public static class PolicySerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Deserializes a <see cref="Policy"/> from a JSON string.
    /// </summary>
    /// <param name="json">The JSON representation of the policy.</param>
    /// <returns>The deserialized <see cref="Policy"/>.</returns>
    public static Policy DeserializeFromJson(string json)
    {
        return JsonSerializer.Deserialize<Policy>(json, JsonOptions)
               ?? throw new ArgumentException("Unable to deserialize policy from JSON.", nameof(json));
    }

    /// <summary>
    /// Deserializes a <see cref="Policy"/> from a YAML string.
    /// YAML keys should use the same camelCase names as the equivalent JSON representation.
    /// </summary>
    /// <param name="yaml">The YAML representation of the policy.</param>
    /// <returns>The deserialized <see cref="Policy"/>.</returns>
    public static Policy DeserializeFromYaml(string yaml)
    {
        var json = ConvertYamlToJson(yaml);
        return DeserializeFromJson(json);
    }

    /// <summary>
    /// Serializes a <see cref="Policy"/> to a JSON string.
    /// </summary>
    /// <param name="policy">The policy to serialize.</param>
    /// <returns>The JSON representation of the policy.</returns>
    public static string SerializeToJson(Policy policy)
    {
        return JsonSerializer.Serialize(policy);
    }

    /// <summary>
    /// Serializes a <see cref="Policy"/> to a YAML string.
    /// YAML keys will use the same camelCase names as the equivalent JSON representation.
    /// </summary>
    /// <param name="policy">The policy to serialize.</param>
    /// <returns>The YAML representation of the policy.</returns>
    public static string SerializeToYaml(Policy policy)
    {
        // Serialize to JSON first, then convert to YAML preserving camelCase key names.
        var json = SerializeToJson(policy);
        return ConvertJsonToYaml(json);
    }

    /// <summary>
    /// Converts a JSON string to YAML format, preserving key names and value types.
    /// </summary>
    private static string ConvertJsonToYaml(string json)
    {
        // JSON is valid YAML, so we can parse it via YamlDotNet's representation model
        // which preserves the original key names.
        var stream = new YamlStream();
        using var reader = new StringReader(json);
        stream.Load(reader);

        var root = stream.Documents[0].RootNode;

        var serializer = new SerializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .Build();

        return serializer.Serialize(root);
    }

    /// <summary>
    /// Converts a YAML string to a JSON string, correctly inferring scalar types.
    /// </summary>
    private static string ConvertYamlToJson(string yaml)
    {
        var stream = new YamlStream();
        using var reader = new StringReader(yaml);
        stream.Load(reader);

        if (stream.Documents.Count == 0)
            throw new ArgumentException("YAML document is empty.", nameof(yaml));

        var sb = new StringBuilder();
        ConvertNodeToJson(stream.Documents[0].RootNode, sb);
        return sb.ToString();
    }

    private static void ConvertNodeToJson(YamlNode node, StringBuilder sb)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                sb.Append('{');
                bool firstEntry = true;
                foreach (var (key, value) in mapping)
                {
                    if (!firstEntry) sb.Append(',');
                    sb.Append(JsonSerializer.Serialize(((YamlScalarNode)key).Value));
                    sb.Append(':');
                    ConvertNodeToJson(value, sb);
                    firstEntry = false;
                }
                sb.Append('}');
                break;

            case YamlSequenceNode sequence:
                sb.Append('[');
                bool firstItem = true;
                foreach (var item in sequence)
                {
                    if (!firstItem) sb.Append(',');
                    ConvertNodeToJson(item, sb);
                    firstItem = false;
                }
                sb.Append(']');
                break;

            case YamlScalarNode scalar:
                sb.Append(ConvertScalarToJson(scalar));
                break;

            default:
                throw new InvalidOperationException($"Unsupported YAML node type: {node.GetType().Name}");
        }
    }

    private static string ConvertScalarToJson(YamlScalarNode scalar)
    {
        var value = scalar.Value;

        // Explicitly quoted strings stay as strings.
        if (scalar.Style is YamlDotNet.Core.ScalarStyle.SingleQuoted
                          or YamlDotNet.Core.ScalarStyle.DoubleQuoted
                          or YamlDotNet.Core.ScalarStyle.Literal
                          or YamlDotNet.Core.ScalarStyle.Folded)
        {
            return JsonSerializer.Serialize(value);
        }

        if (value is null or "null" or "~")
            return "null";
        if (value is "true" or "True" or "TRUE")
            return "true";
        if (value is "false" or "False" or "FALSE")
            return "false";
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longVal))
            return longVal.ToString(CultureInfo.InvariantCulture);
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleVal))
            return doubleVal.ToString("G", CultureInfo.InvariantCulture);

        return JsonSerializer.Serialize(value);
    }
}
