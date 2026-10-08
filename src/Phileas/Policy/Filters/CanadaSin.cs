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

/// <summary>
///     Policy configuration for detecting Canadian Social Insurance Numbers (SIN), nine Luhn-valid digits
///     written canonically as <c>NNN-NNN-NNN</c>.
/// </summary>
public class CanadaSin : AbstractPolicyFilter
{
    /// <summary>Gets or sets the list of SIN filter strategies to apply.</summary>
    [JsonPropertyName("canadaSinFilterStrategies")]
    public List<CanadaSinFilterStrategy>? Strategies { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to drop matches beginning with 0 or 8, which are not issued to
    ///     individuals (8 is used for business numbers, which also pass the Luhn check). Defaults to
    ///     <see langword="false" /> (match any Luhn-valid nine-digit value), since the rule rests on secondary sources.
    /// </summary>
    [JsonPropertyName("onlyValidPrefixes")]
    public bool OnlyValidPrefixes { get; set; } = false;
}
