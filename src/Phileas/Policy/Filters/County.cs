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

/// <summary>Policy configuration for the County dictionary filter.</summary>
public class County : AbstractPolicyFilter
{
    /// <summary>Gets or sets the list of County filter strategies to apply.</summary>
    [JsonPropertyName("countyFilterStrategies")]
    public List<CountyFilterStrategy>? Strategies { get; set; }

    /// <summary>Gets or sets a value indicating whether fuzzy matching is enabled. Defaults to <see langword="false" />.</summary>
    [JsonPropertyName("fuzzy")]
    public bool Fuzzy { get; set; } = false;

    /// <summary>Gets or sets the fuzzy-matching sensitivity. Defaults to <c>"medium"</c>.</summary>
    [JsonPropertyName("sensitivity")]
    public string Sensitivity { get; set; } = "medium";

    /// <summary>Gets or sets a value indicating whether a match must be capitalized. Defaults to <see langword="false" />.</summary>
    [JsonPropertyName("capitalized")]
    public bool Capitalized { get; set; } = false;
}
