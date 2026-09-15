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

namespace Phileas.Policy;

/// <summary>
///     Provides methods to serialize and deserialize a <see cref="Policy" /> to and from
///     JSON.
/// </summary>
public static class PolicySerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // Mirror the Java reference (gson), which omits null fields. The canonical policy schema is
        // additionalProperties:false and types optional objects (crypto, fpe) as objects, so emitting
        // an explicit null would make otherwise-valid policies fail schema validation.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Resolve whole-value ${NAME} placeholders to environment variables on deserialization.
        Converters = { new PlaceholderStringConverter() }
    };

    /// <summary>
    ///     Deserializes a <see cref="Policy" /> from a JSON string, validating it against the redaction
    ///     policy schema first.
    /// </summary>
    /// <param name="json">The JSON representation of the policy.</param>
    /// <param name="validate">
    ///     Whether to validate against the schema. On by default: a key the schema does not define is a
    ///     policy that does not do what it says, and reporting it beats loading it. Pass
    ///     <see langword="false" /> to load a policy written for a different schema version, accepting
    ///     that whatever the schema would have rejected is skipped rather than applied.
    /// </param>
    /// <returns>The deserialized <see cref="Policy" />.</returns>
    /// <exception cref="PolicyValidationException">
    ///     The policy does not match the schema and <paramref name="validate" /> is
    ///     <see langword="true" />.
    /// </exception>
    public static Policy DeserializeFromJson(string json, bool validate = true)
    {
        // Validated before binding, not after: System.Text.Json skips a key it does not recognise, so a
        // misspelled filter used to load as an absent one and simply never run. The policy was accepted
        // and then quietly did not do what it said.
        if (validate)
        {
            // Normalized first, so the spellings this port documents as accepted (any casing, the
            // SHIFT_DATE alias, the deprecated dictionary key) are not reported as schema errors. What
            // is left is what the policy genuinely got wrong.
            var errors = PolicySchema.GetValidationErrors(json, normalize: true);
            if (errors.Count > 0) throw new PolicyValidationException(errors);
        }

        return JsonSerializer.Deserialize<Policy>(json, JsonOptions)
               ?? throw new ArgumentException("Unable to deserialize policy from JSON.", nameof(json));
    }

    /// <summary>
    ///     Serializes a <see cref="Policy" /> to a JSON string.
    /// </summary>
    /// <param name="policy">The policy to serialize.</param>
    /// <returns>The JSON representation of the policy.</returns>
    public static string SerializeToJson(Policy policy)
    {
        return JsonSerializer.Serialize(policy, JsonOptions);
    }

}