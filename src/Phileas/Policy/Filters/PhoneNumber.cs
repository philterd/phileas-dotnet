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
///     Policy configuration for detecting phone numbers.
/// </summary>
public class PhoneNumber : AbstractPolicyFilter
{
    /// <summary>The default region used to interpret phone numbers written without an international "+" country code.</summary>
    public const string DefaultRegion = "US";

    /// <summary>Gets or sets the list of phone number filter strategies to apply.</summary>
    [JsonPropertyName("phoneNumberFilterStrategies")]
    public List<PhoneNumberFilterStrategy>? Strategies { get; set; }

    /// <summary>
    ///     Gets or sets the default region(s), ISO 3166-1 alpha-2, used to detect national-format phone numbers
    ///     written without an international "+" country code. The policy may give either a single string or an
    ///     array of strings; both bind here. Numbers with a "+" prefix are detected regardless of this value.
    ///     Requires policy schema 1.2.0.
    /// </summary>
    [JsonPropertyName("region")]
    [JsonConverter(typeof(StringOrStringListConverter))]
    public List<string>? Region { get; set; }

    /// <summary>
    ///     Gets the configured regions, or a single <see cref="DefaultRegion" /> when the policy set none.
    /// </summary>
    /// <returns>The regions to scan for national-format phone numbers.</returns>
    public List<string> GetRegionOrDefault()
    {
        return Region is { Count: > 0 } ? Region : new List<string> { DefaultRegion };
    }
}