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
///     A named set of literal terms (and term files) that should be excluded from filtering. Mirrors the
///     canonical Phileas <c>ignored</c> entry.
/// </summary>
public class Ignored
{
    /// <summary>Gets or sets the name of this ignored set.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets the literal terms to ignore.</summary>
    [JsonPropertyName("terms")]
    public List<string> Terms { get; set; } = new();

    /// <summary>Gets or sets the files whose contents provide additional terms to ignore.</summary>
    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();

    /// <summary>
    ///     Gets or sets a value indicating whether term comparison is case-sensitive. Defaults to
    ///     <see langword="false" />.
    /// </summary>
    [JsonPropertyName("caseSensitive")]
    public bool CaseSensitive { get; set; } = false;
}
