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

namespace Phileas.Policy;

/// <summary>
///     Analysis configuration controlling how the policy reports detected entities.
/// </summary>
public class Analysis
{
    /// <summary>
    ///     Gets or sets a value indicating whether identification analysis is enabled. Defaults to
    ///     <see langword="true" />.
    /// </summary>
    [JsonPropertyName("identification")]
    public bool Identification { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether span disambiguation runs for this policy. Defaults to
    ///     <see langword="true" />, which is the behavior of a policy that does not mention it.
    ///     <para>
    ///         Disambiguation is the context-vector step that decides which type an ambiguously typed
    ///         span is, an SSN against a phone number for instance. Setting this to
    ///         <see langword="false" /> skips it for this policy, leaving each competing span to be
    ///         resolved by the overlap rules alone.
    ///     </para>
    ///     <para>
    ///         Subject to the host having enabled disambiguation at all: a policy can decline it but
    ///         cannot turn it on, since a host that disabled it supplies a service that does nothing.
    ///     </para>
    /// </summary>
    [JsonPropertyName("spanDisambiguation")]
    public bool SpanDisambiguation { get; set; } = true;
}
