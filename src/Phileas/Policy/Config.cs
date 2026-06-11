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
///     Global configuration settings that apply to all filters within a policy.
/// </summary>
public class Config
{
    /// <summary>Gets or sets the document-splitting configuration.</summary>
    [JsonPropertyName("splitting")]
    public Splitting Splitting { get; set; } = new();

    /// <summary>Gets or sets the PDF redaction configuration.</summary>
    [JsonPropertyName("pdf")]
    public Pdf Pdf { get; set; } = new();

    /// <summary>Gets or sets the post-filter configuration that trims trailing characters from detected spans.</summary>
    [JsonPropertyName("postFilters")]
    public PostFilters PostFilters { get; set; } = new();

    /// <summary>Gets or sets the analysis configuration.</summary>
    [JsonPropertyName("analysis")]
    public Analysis Analysis { get; set; } = new();
}