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

using System.Text.Json.Serialization;
using Philterd.PhiSql;

namespace Phileas.Policy;

/// <summary>
///     Root configuration object for a Phileas policy. A policy defines which entity types to detect,
///     how to replace each type, which values to ignore, and global settings such as window size and
///     encryption keys.
/// </summary>
public class Policy
{
    /// <summary>
    ///     Creates a <see cref="Policy" /> from a PhiSQL document. The PhiSQL is compiled to a Phileas
    ///     JSON policy by the <c>phisql</c> compiler and then deserialized into a <see cref="Policy" />;
    ///     the runtime behaves identically to a policy loaded from JSON — PhiSQL is purely an additional
    ///     authoring format.
    /// </summary>
    /// <param name="phisql">The PhiSQL document source.</param>
    /// <returns>The compiled <see cref="Policy" />.</returns>
    /// <exception cref="PolicyCompilationException">If the PhiSQL cannot be parsed or compiled.</exception>
    public static Policy FromPhiSQL(string phisql)
    {
        CompileResult result;
        try
        {
            result = new Compiler().Compile(phisql);
        }
        catch (Exception ex) when (ex is ParseException or CompileException)
        {
            // ParseException for syntax errors, CompileException for semantic ones (unknown entity
            // type, strategy, and so on). Wrap them in a Phileas type so callers get one exception to
            // catch and the original message is kept.
            throw new PolicyCompilationException(
                "The PhiSQL document could not be compiled into a policy: " + ex.Message, ex);
        }

        return PolicySerializer.DeserializeFromJson(result.ToJsonString());
    }

    /// <summary>
    ///     Gets or sets the human-readable name of the policy. This is an in-memory convenience label only:
    ///     the canonical Phileas policy JSON has no top-level <c>name</c> (the name is tracked separately —
    ///     e.g. via the PhiSQL <c>POLICY</c> declaration or the source filename), so it is never serialized
    ///     to or read from JSON.
    /// </summary>
    [JsonIgnore]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the global configuration settings for the policy.</summary>
    [JsonPropertyName("config")]
    public Config Config { get; set; } = new();

    /// <summary>Gets or sets the AES encryption settings used by the <c>CRYPTO_REPLACE</c> strategy.</summary>
    [JsonPropertyName("crypto")]
    public Crypto? Crypto { get; set; }

    /// <summary>Gets or sets the format-preserving encryption settings used by the <c>FPE_ENCRYPT_REPLACE</c> strategy.</summary>
    [JsonPropertyName("fpe")]
    public Fpe? Fpe { get; set; }

    /// <summary>Gets or sets the collection of filter identifiers that specify which entity types to detect.</summary>
    [JsonPropertyName("identifiers")]
    public Identifiers Identifiers { get; set; } = new();

    /// <summary>Gets or sets the list of exact values that should not be filtered.</summary>
    [JsonPropertyName("ignored")]
    public List<Ignored> Ignored { get; set; } = new();

    /// <summary>Gets or sets the list of regex-based patterns for values that should not be filtered.</summary>
    [JsonPropertyName("ignoredPatterns")]
    public List<IgnoredPattern> IgnoredPatterns { get; set; } = new();

    /// <summary>Gets or sets the graphical redaction configuration (fixed bounding boxes).</summary>
    [JsonPropertyName("graphical")]
    public Graphical Graphical { get; set; } = new();

    /// <summary>
    ///     Gets or sets the named, reusable replacement generators referenced by <c>MAP_REPLACE</c> filter strategies
    ///     (keyed by generator name). A <c>MAP_REPLACE</c> strategy references a generator by name via its
    ///     <c>generator</c> property to produce a replacement for a detected value absent from its lookup table.
    /// </summary>
    [JsonPropertyName("generators")]
    public Dictionary<string, Generator>? Generators { get; set; }
}