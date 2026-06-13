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

namespace Phileas.Policy.Filters;

/// <summary>
///     Connection and behavior configuration for the PhEye NLP service.
/// </summary>
public class PhEyeConfiguration
{
    /// <summary>Gets or sets the base URL of the PhEye service. Defaults to <c>http://localhost:8080</c>.</summary>
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = "http://localhost:8080";

    /// <summary>Gets or sets the Bearer token used for authenticating requests to the PhEye service.</summary>
    [JsonPropertyName("bearerToken")]
    public string? BearerToken { get; set; }

    /// <summary>Gets or sets the request timeout in seconds. Defaults to 30.</summary>
    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 30;

    /// <summary>
    ///     Gets or sets the list of entity labels the filter should detect (e.g. <c>"Person"</c>). Defaults to
    ///     <c>["Person"]</c>. For a local GLiNER model these are the detection prompt.
    /// </summary>
    [JsonPropertyName("labels")]
    public List<string> Labels { get; set; } = new() { "Person" };

    /// <summary>
    ///     Gets or sets the filesystem path to a local GLiNER model directory (the ONNX model, the SentencePiece
    ///     tokenizer, and the GLiNER config). When set, entities are detected with on-device inference instead of via
    ///     <see cref="Endpoint" />. Matches PhiSQL schema 1.1.0 <c>modelPath</c> (the <c>MODEL</c> clause).
    /// </summary>
    [JsonPropertyName("modelPath")]
    public string? ModelPath { get; set; }

    /// <summary>
    ///     Gets or sets the minimum span confidence for the local model to return a detection. Defaults to 0.5.
    ///     Applies to local inference only. Matches PhiSQL schema 1.1.0 <c>threshold</c>.
    /// </summary>
    [JsonPropertyName("threshold")]
    public double Threshold { get; set; } = 0.5;
}