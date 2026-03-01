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
    ///     <c>["Person"]</c>.
    /// </summary>
    [JsonPropertyName("labels")]
    public List<string> Labels { get; set; } = new() { "Person" };
}