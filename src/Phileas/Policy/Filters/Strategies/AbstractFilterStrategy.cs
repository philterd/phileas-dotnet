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

namespace Phileas.Policy.Filters.Strategies;

/// <summary>
///     Base class for all policy-level filter strategy objects deserialized from a Phileas policy
///     JSON document. Defines the common replacement strategy properties shared by all concrete strategies.
/// </summary>
public abstract class AbstractFilterStrategy
{
    // These string constants are self-documenting canonical policy tokens (strategy names, replacement
    // scopes, condition tokens and operators) ported from the Java AbstractFilterStrategy. They are the
    // canonical values of the policy schema's "strategy" enums; SchemaConformanceTest asserts every
    // schema strategy has a matching constant here.
#pragma warning disable CS1591 // self-documenting string constants
    /// <summary>The default redaction format. The placeholder <c>%t</c> is replaced with the filter-type slug.</summary>
    public const string DefaultRedaction = "{{{REDACTED-%t}}}";

    // Replacement strategy names.
    public const string Redact = "REDACT";
    public const string RandomReplace = "RANDOM_REPLACE";
    public const string StaticReplace = "STATIC_REPLACE";
    public const string CryptoReplace = "CRYPTO_REPLACE";
    public const string FpeEncryptReplace = "FPE_ENCRYPT_REPLACE";
    public const string HashSha256Replace = "HASH_SHA256_REPLACE";
    public const string Last4 = "LAST_4";
    public const string Mask = "MASK";
    public const string Same = "SAME";
    public const string Truncate = "TRUNCATE";
    public const string Leading = "LEADING";
    public const string Trailing = "TRAILING";
    public const string Abbreviate = "ABBREVIATE";
    public const string TruncateToYear = "TRUNCATE_TO_YEAR";
    public const string Shift = "SHIFT";
    public const string Relative = "RELATIVE";

    // Replacement scope.
    public const string ReplacementScopeDocument = "DOCUMENT";
    public const string ReplacementScopeContext = "CONTEXT";

    // Condition tokens.
    public const string Token = "token";
    public const string Context = "context";
    public const string Confidence = "confidence";
    public const string Birthdate = "birthdate";
    public const string Deathdate = "deathdate";
    public const string BirthdateOrDeathdate = "birthdate or deathdate";

    // Condition operators.
    public const string Startswith = "startswith";
    public const string EqualsOp = "==";
    public const string NotEquals = "!=";
    public const string GreaterThan = ">";
    public const string LessThan = "<";
    public const string GreaterThanEquals = ">=";
    public const string LessThanEquals = "<=";
    public const string Is = "is";
    public const string IsNot = "is not";
#pragma warning restore CS1591

    /// <summary>
    ///     Gets or sets the replacement strategy name (e.g. <c>"REDACT"</c>, <c>"STATIC_REPLACE"</c>). Defaults to
    ///     <c>"REDACT"</c>.
    /// </summary>
    [JsonPropertyName("strategy")]
    public string Strategy { get; set; } = Redact;

    /// <summary>
    ///     Gets or sets the redaction format string. The placeholder <c>%t</c> is replaced with the filter-type slug.
    ///     Defaults to <c>"{{{REDACTED-%t}}}"</c>.
    /// </summary>
    [JsonPropertyName("redactionFormat")]
    public string RedactionFormat { get; set; } = "{{{REDACTED-%t}}}";

    /// <summary>Gets or sets the static replacement value used by the <c>STATIC_REPLACE</c> strategy.</summary>
    [JsonPropertyName("staticReplacement")]
    public string? StaticReplacement { get; set; }

    /// <summary>Gets or sets the character used for masking when the <c>MASK</c> strategy is active. Defaults to <c>"*"</c>.</summary>
    [JsonPropertyName("maskCharacter")]
    public string MaskCharacter { get; set; } = "*";

    /// <summary>
    ///     Gets or sets the mask length for the <c>MASK</c> strategy. Use <c>"same"</c> to match the entity length, or a
    ///     numeric string for a fixed length.
    /// </summary>
    [JsonPropertyName("maskLength")]
    public string MaskLength { get; set; } = "same";

    /// <summary>
    ///     Gets or sets an optional condition expression. When <see langword="null" /> or empty the strategy always
    ///     applies.
    /// </summary>
    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether a random cryptographic salt is used. Defaults to
    ///     <see langword="false" />.
    /// </summary>
    [JsonPropertyName("salt")]
    public bool Salt { get; set; } = false;

    /// <summary>
    ///     Gets or sets how the <c>RANDOM_REPLACE</c> strategy produces replacement values:
    ///     <c>"realistic"</c> (default), <c>"from_list"</c>, or <c>"uuid"</c>.
    /// </summary>
    [JsonPropertyName("anonymizationMethod")]
    public string? AnonymizationMethod { get; set; }

    /// <summary>
    ///     Gets or sets the candidate values the <c>RANDOM_REPLACE</c> strategy draws from. When
    ///     non-empty, replacements are taken from this list.
    /// </summary>
    [JsonPropertyName("anonymizationCandidates")]
    public List<string>? AnonymizationCandidates { get; set; }

    /// <summary>
    ///     Gets or sets the scope over which a <c>RANDOM_REPLACE</c> replacement is reused.
    ///     <see cref="ReplacementScopeContext" /> reuses a previously generated replacement for the same
    ///     token (referential integrity across the context); <see cref="ReplacementScopeDocument" /> (the
    ///     default) anonymizes each occurrence independently.
    /// </summary>
    [JsonPropertyName("replacementScope")]
    public string ReplacementScope { get; set; } = ReplacementScopeDocument;
}