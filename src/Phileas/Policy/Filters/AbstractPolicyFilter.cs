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
using Phileas.Policy;

namespace Phileas.Policy.Filters;

/// <summary>
/// Base class for all policy-level filter configuration objects that are deserialized from a
/// Phileas policy JSON document. Provides the common properties shared by every filter type.
/// </summary>
public abstract class AbstractPolicyFilter
{
    /// <summary>Gets or sets the list of literal values that the filter should ignore.</summary>
    [JsonPropertyName("ignored")]
    public List<string>? Ignored { get; set; }

    /// <summary>Gets or sets the list of file paths whose contents provide additional ignored values.</summary>
    [JsonPropertyName("ignoredFiles")]
    public List<string>? IgnoredFiles { get; set; }

    /// <summary>Gets or sets the list of regular-expression patterns whose matches the filter should ignore.</summary>
    [JsonPropertyName("ignoredPatterns")]
    public List<IgnoredPattern>? IgnoredPatterns { get; set; }

    /// <summary>Gets or sets the detection sensitivity level (<c>"low"</c>, <c>"medium"</c>, or <c>"high"</c>). Defaults to <c>"medium"</c>.</summary>
    [JsonPropertyName("sensitivity")]
    public string Sensitivity { get; set; } = "medium";

    /// <summary>Gets or sets the filter priority. Higher values are applied before lower values when resolving overlapping spans. Defaults to 0.</summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 0;
}
