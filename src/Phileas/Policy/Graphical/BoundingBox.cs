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
using Phileas.Policy.Filters;

namespace Phileas.Policy;

/// <summary>
///     A fixed rectangular region to redact in a graphical (e.g. PDF) document.
/// </summary>
public class BoundingBox : AbstractPolicyFilter
{
    /// <summary>Gets or sets the redaction color, or <see langword="null" /> for the default.</summary>
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    /// <summary>Gets or sets the x coordinate of the box.</summary>
    [JsonPropertyName("x")]
    public float X { get; set; }

    /// <summary>Gets or sets the y coordinate of the box.</summary>
    [JsonPropertyName("y")]
    public float Y { get; set; }

    /// <summary>Gets or sets the width of the box.</summary>
    [JsonPropertyName("w")]
    public float W { get; set; }

    /// <summary>Gets or sets the height of the box.</summary>
    [JsonPropertyName("h")]
    public float H { get; set; }

    /// <summary>Gets or sets the 1-based page number the box applies to. Defaults to 1.</summary>
    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;
}
