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
///     Policy configuration for detecting email addresses.
/// </summary>
public class EmailAddress : AbstractPolicyFilter
{
    /// <summary>Gets or sets the list of email address filter strategies to apply.</summary>
    [JsonPropertyName("emailAddressFilterStrategies")]
    public List<EmailAddressFilterStrategy>? Strategies { get; set; }

    /// <summary>Gets or sets a value indicating whether the strict RFC-shaped pattern is used instead of the lenient one. Defaults to <see langword="true" />.</summary>
    [JsonPropertyName("onlyStrictMatches")]
    public bool OnlyStrictMatches { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the domain's top-level domain must be an IANA-registered one. Defaults to <see langword="false" />.</summary>
    [JsonPropertyName("onlyValidTLDs")]
    public bool OnlyValidTLDs { get; set; } = false;
}