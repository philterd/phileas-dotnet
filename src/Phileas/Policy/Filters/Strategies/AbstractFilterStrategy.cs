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
    /// <summary>
    ///     Gets or sets the replacement strategy name (e.g. <c>"REDACT"</c>, <c>"STATIC_REPLACE"</c>). Defaults to
    ///     <c>"REDACT"</c>.
    /// </summary>
    [JsonPropertyName("strategy")]
    public string Strategy { get; set; } = "REDACT";

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
}