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
///     Policy configuration for detecting US Individual Taxpayer Identification Numbers (ITIN), SSN-shaped values
///     beginning with 9 (<c>9XX-XX-XXXX</c>).
/// </summary>
public class Itin : AbstractPolicyFilter
{
    /// <summary>Gets or sets the list of ITIN filter strategies to apply.</summary>
    [JsonPropertyName("itinFilterStrategies")]
    public List<ItinFilterStrategy>? Strategies { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to keep only matches whose fourth and fifth digits fall in the
    ///     ranges the IRS issues (50 to 65, 70 to 88, 90 to 92, 94 to 99). Defaults to <see langword="false" />
    ///     (match any <c>9XX-XX-XXXX</c> value), so a range issued after this build is still detected. Setting it
    ///     to <see langword="true" /> also drops ATINs, whose fourth and fifth digits are 93.
    /// </summary>
    [JsonPropertyName("onlyValidRanges")]
    public bool OnlyValidRanges { get; set; } = false;
}
