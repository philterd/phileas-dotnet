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
///     Policy configuration for detecting US Employer Identification Numbers (EIN), the federal tax ID
///     written canonically as <c>NN-NNNNNNN</c>.
/// </summary>
public class Ein : AbstractPolicyFilter
{
    /// <summary>Gets or sets the list of EIN filter strategies to apply.</summary>
    [JsonPropertyName("einFilterStrategies")]
    public List<EinFilterStrategy>? Strategies { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to keep only matches whose two-digit prefix is one the IRS
    ///     currently issues. Defaults to <see langword="false" /> (match any EIN-formatted value), so a prefix
    ///     added after this build still matches; set to <see langword="true" /> to reduce false positives on
    ///     format-valid but non-issued numbers.
    /// </summary>
    [JsonPropertyName("onlyValidPrefixes")]
    public bool OnlyValidPrefixes { get; set; } = false;
}
