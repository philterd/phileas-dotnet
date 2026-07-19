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
///     A named replacement generator referenced by a <c>MAP_REPLACE</c> filter strategy. A generator produces a
///     replacement for a detected value that is absent from the strategy's lookup table. Generators target a local
///     model endpoint inside the deployment boundary so detected values are not sent to a third party.
/// </summary>
public class Generator
{
    /// <summary>Generator backend value: calls a local Ollama-compatible generate endpoint.</summary>
    public const string TypeOllama = "ollama";

    /// <summary>Gets or sets the generator backend. <c>"ollama"</c> calls a local Ollama-compatible generate endpoint.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Gets or sets the base URL of the local generator endpoint (e.g. <c>http://localhost:11434</c>).</summary>
    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; set; }

    /// <summary>Gets or sets the model name the endpoint should use (e.g. <c>llama3.1</c>).</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    ///     Gets or sets the prompt template. The placeholder <c>{{token}}</c> is replaced with the detected value and
    ///     <c>{{label}}</c> with its entity label. The model must return only the replacement value.
    /// </summary>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; set; }

    /// <summary>
    ///     Gets or sets the maximum time in milliseconds to wait for the generator before falling back to the strategy's
    ///     fallback strategy. Required so a generator can never block the pipeline indefinitely.
    /// </summary>
    [JsonPropertyName("timeoutMs")]
    public int? TimeoutMs { get; set; }
}
