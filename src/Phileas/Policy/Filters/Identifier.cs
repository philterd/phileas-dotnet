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
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

/// <summary>Policy configuration for a custom regex-based identifier filter.</summary>
public class Identifier : AbstractPolicyFilter
{
    /// <summary>The default identifier pattern: a run of 6+ uppercase letters, digits, underscores or hyphens.</summary>
    public const string DefaultIdentifierRegex = @"\b[A-Z0-9_-]{6,}\b";

    /// <summary>Gets or sets the list of identifier filter strategies to apply.</summary>
    [JsonPropertyName("identifierFilterStrategies")]
    public List<IdentifierFilterStrategy>? Strategies { get; set; }

    /// <summary>Gets or sets the regex pattern that identifies the entity. Defaults to <see cref="DefaultIdentifierRegex" />.</summary>
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = DefaultIdentifierRegex;

    /// <summary>Gets or sets the capture-group number whose match forms the span (0 = whole match). Defaults to 0.</summary>
    [JsonPropertyName("groupNumber")]
    public int GroupNumber { get; set; } = 0;

    /// <summary>Gets or sets a value indicating whether matching is case-sensitive. Defaults to <see langword="true" />.</summary>
    [JsonPropertyName("caseSensitive")]
    public bool CaseSensitive { get; set; } = true;

    /// <summary>Gets or sets the classification label applied to matches. Defaults to <c>"custom-identifier"</c>.</summary>
    [JsonPropertyName("classification")]
    public string Classification { get; set; } = "custom-identifier";

    /// <summary>
    ///     Gets or sets an optional post-match validator. A match is kept only if the named validator
    ///     passes, so a generic identifier can reject format-valid but checksum-invalid values. Accepts
    ///     both the schema's string form and object form.
    /// </summary>
    [JsonPropertyName("validator")]
    [JsonConverter(typeof(ValidatorJsonConverter))]
    public Validator? Validator { get; set; }
}
