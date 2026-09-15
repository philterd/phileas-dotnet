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
///     Policy configuration for detecting package tracking numbers.
/// </summary>
public class TrackingNumber : AbstractPolicyFilter
{
    /// <summary>Gets or sets the list of tracking number filter strategies to apply.</summary>
    [JsonPropertyName("trackingNumberFilterStrategies")]
    public List<TrackingNumberFilterStrategy>? Strategies { get; set; }

    /// <summary>Gets or sets a value indicating whether UPS tracking numbers are detected. Defaults to <see langword="true" />.</summary>
    [JsonPropertyName("ups")]
    public bool Ups { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether FedEx tracking numbers are detected. Defaults to <see langword="true" />.</summary>
    [JsonPropertyName("fedex")]
    public bool Fedex { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether USPS tracking numbers are detected. Defaults to <see langword="true" />.</summary>
    [JsonPropertyName("usps")]
    public bool Usps { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether a number may be written in space-separated groups. Defaults to <see langword="false" />.</summary>
    [JsonPropertyName("allowSpaces")]
    public bool AllowSpaces { get; set; } = false;
}