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
/// Root configuration object for a Phileas policy. A policy defines which entity types to detect,
/// how to replace each type, which values to ignore, and global settings such as window size and
/// encryption keys.
/// </summary>
public class Policy
{
    /// <summary>Gets or sets the human-readable name of the policy.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the global configuration settings for the policy.</summary>
    [JsonPropertyName("config")]
    public Config Config { get; set; } = new Config();

    /// <summary>Gets or sets the AES encryption settings used by the <c>CRYPTO_REPLACE</c> strategy.</summary>
    [JsonPropertyName("crypto")]
    public Crypto? Crypto { get; set; }

    /// <summary>Gets or sets the format-preserving encryption settings used by the <c>FPE_ENCRYPT_REPLACE</c> strategy.</summary>
    [JsonPropertyName("fpe")]
    public Fpe? Fpe { get; set; }

    /// <summary>Gets or sets the collection of filter identifiers that specify which entity types to detect.</summary>
    [JsonPropertyName("identifiers")]
    public Identifiers Identifiers { get; set; } = new Identifiers();

    /// <summary>Gets or sets the list of exact values that should not be filtered.</summary>
    [JsonPropertyName("ignored")]
    public List<Ignored> Ignored { get; set; } = new List<Ignored>();

    /// <summary>Gets or sets the list of regex-based patterns for values that should not be filtered.</summary>
    [JsonPropertyName("ignoredPatterns")]
    public List<IgnoredPattern> IgnoredPatterns { get; set; } = new List<IgnoredPattern>();

    /// <summary>Gets or sets the graphical redaction configuration (e.g. for PDF redaction).</summary>
    [JsonPropertyName("graphical")]
    public Graphical Graphical { get; set; } = new Graphical();

    [JsonPropertyName("postFilters")]
    public PostFilters PostFilters { get; set; } = new PostFilters();
}
