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
using Json.Schema;

namespace Phileas.Policy;

/// <summary>
///     Describes the redaction policy JSON schema that this release of Phileas supports. Each Phileas
///     release supports exactly one schema version. The canonical schema version is owned and versioned
///     by the <c>Philterd.PhiSql</c> dependency.
/// </summary>
public static class PolicySchema
{
    /// <summary>Returns the redaction policy schema as a JSON string.</summary>
    public static string GetSchema()
    {
        return global::Philterd.PhiSql.PolicySchema.GetSchema();
    }

    /// <summary>Returns the redaction policy schema version supported by this release of Phileas.</summary>
    public static string GetSupportedSchemaVersion()
    {
        return global::Philterd.PhiSql.PolicySchema.GetSupportedSchemaVersion();
    }

    /// <summary>
    ///     Validates a JSON policy against the schema.
    /// </summary>
    /// <param name="jsonPolicy">The JSON policy to validate.</param>
    /// <returns><c>true</c> if the policy is valid; otherwise <c>false</c>.</returns>
    public static bool Validate(string jsonPolicy)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(jsonPolicy);
        }
        catch (Exception)
        {
            // The policy is not well-formed JSON. That is an invalid policy, not a validator failure.
            return false;
        }

        try
        {
            // The schema declares its own draft via $schema; JsonSchema.Net honors it rather than
            // assuming an older draft that would silently under-enforce newer keywords.
            var schema = JsonSchema.FromText(GetSchema());
            var results = schema.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.Flag });
            return results.IsValid;
        }
        catch (Exception ex)
        {
            // A failure here means validation itself could not run (an unreadable schema, an unsupported
            // draft, and so on) - not that the policy is invalid. Surface it rather than silently
            // reporting the policy as invalid, which would mask a real defect in the schema or its wiring.
            throw new InvalidOperationException(
                "Could not validate the policy against the redaction policy schema.", ex);
        }
    }
}
