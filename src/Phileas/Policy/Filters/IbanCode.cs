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
///     Policy configuration for detecting International Bank Account Numbers (IBAN).
/// </summary>
public class IbanCode : AbstractPolicyFilter
{
    /// <summary>Gets or sets the list of IBAN code filter strategies to apply.</summary>
    [JsonPropertyName("ibanCodeFilterStrategies")]
    public List<IbanCodeFilterStrategy>? Strategies { get; set; }

    /// <summary>Gets or sets a value indicating whether a code may be written in space-separated groups. Defaults to <see langword="true" />.</summary>
    [JsonPropertyName("allowSpaces")]
    public bool AllowSpaces { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether only codes passing the MOD-97-10 checksum are detected. Defaults to <see langword="true" />.</summary>
    [JsonPropertyName("onlyValidIBANCodes")]
    public bool OnlyValidIBANCodes { get; set; } = true;
}