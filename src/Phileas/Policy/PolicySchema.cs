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

    // Evaluation is serialized on this. A compiled JsonSchema is not safe to evaluate from several
    // threads at once: measured on JsonSchema.Net 7.0.4, roughly one concurrent evaluation in seven
    // reported an invalid policy as valid. A false "valid" is the one answer this must never give, so
    // the schema is compiled once and evaluated under a lock, which measured 1.9 ms per call against
    // 7.2 ms for compiling a fresh copy each time.
    private static readonly object EvaluationGate = new();

    private static readonly Lazy<JsonSchema> CompiledSchema = new(() =>
    {
        // The schema declares its own draft via $schema; JsonSchema.Net honors it rather than
        // assuming an older draft that would silently under-enforce newer keywords.
        try
        {
            return JsonSchema.FromText(GetSchema());
        }
        catch (Exception ex)
        {
            // Validation could not run (an unreadable schema, an unsupported draft, and so on), which
            // is not the same as the policy being invalid. Surface it rather than reporting every
            // policy as invalid, which would mask a defect in the schema or its wiring.
            throw new InvalidOperationException(
                "Could not read the redaction policy schema.", ex);
        }
    });

    /// <summary>
    ///     Validates a JSON policy against the schema.
    /// </summary>
    /// <param name="jsonPolicy">The JSON policy to validate.</param>
    /// <returns><c>true</c> if the policy is valid; otherwise <c>false</c>.</returns>
    public static bool Validate(string jsonPolicy)
    {
        return GetValidationErrors(jsonPolicy).Count == 0;
    }

    /// <summary>
    ///     Validates a JSON policy against the schema and describes what failed.
    /// </summary>
    /// <param name="jsonPolicy">The JSON policy to validate.</param>
    /// <returns>
    ///     One entry per failure, as <c>location: reason</c>, where the location is a JSON Pointer into
    ///     the policy. Empty when the policy is valid. A policy that is not well-formed JSON is an
    ///     invalid policy rather than a validator failure, and is reported as one entry.
    /// </returns>
    public static IReadOnlyList<string> GetValidationErrors(string jsonPolicy)
    {
        return GetValidationErrors(jsonPolicy, normalize: false);
    }

    /// <summary>
    ///     Validates a JSON policy against the schema, optionally rewriting the spellings this port
    ///     accepts into the ones the schema declares first.
    /// </summary>
    /// <param name="jsonPolicy">The JSON policy to validate.</param>
    /// <param name="normalize">
    ///     Whether to validate what the policy means to this port rather than its literal text. With
    ///     this on, a strategy name in any casing, the older <c>SHIFT_DATE</c> name and the deprecated
    ///     <c>identifiers.dictionary</c> key are accepted, as they are at runtime. With it off the
    ///     schema is applied as written.
    /// </param>
    /// <returns>One entry per failure, as <c>location: reason</c>. Empty when the policy is valid.</returns>
    internal static IReadOnlyList<string> GetValidationErrors(string jsonPolicy, bool normalize)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(jsonPolicy);
        }
        catch (Exception ex)
        {
            return new[] { "the policy is not well-formed JSON: " + ex.Message };
        }

        if (normalize) node = PolicyNormalizer.Normalize(node);

        EvaluationResults results;
        lock (EvaluationGate)
        {
            results = CompiledSchema.Value.Evaluate(node,
                new EvaluationOptions { OutputFormat = OutputFormat.List });
        }

        if (results.IsValid) return Array.Empty<string>();

        // Distinct: the same failure is reported once per schema branch that rejected it, so a single
        // wrong value can appear several times over.
        var errors = new List<string>();
        foreach (var detail in results.Details)
        {
            if (!detail.HasErrors) continue;
            foreach (var error in detail.Errors!)
            {
                var location = detail.InstanceLocation.ToString();
                var message = (string.IsNullOrEmpty(location) ? "(root)" : location) + ": " + error.Value;
                if (!errors.Contains(message)) errors.Add(message);
            }
        }

        // An empty list here would report a policy the schema rejected as valid, which is the one
        // outcome this method must never produce.
        if (errors.Count == 0) errors.Add("(root): the policy does not match the redaction policy schema");

        return errors;
    }
}
