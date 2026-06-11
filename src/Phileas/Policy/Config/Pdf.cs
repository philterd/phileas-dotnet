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
///     Configures how detected entities are redacted in PDF documents.
/// </summary>
public class Pdf
{
    /// <summary>Gets or sets the redaction box color. Defaults to <c>"black"</c>.</summary>
    [JsonPropertyName("redactionColor")]
    public string RedactionColor { get; set; } = "black";

    /// <summary>Gets or sets a value indicating whether the replacement text is shown over the redaction. Defaults to <see langword="false" />.</summary>
    [JsonPropertyName("showReplacement")]
    public bool ShowReplacement { get; set; } = false;

    /// <summary>Gets or sets the font used for replacement text. Defaults to <c>"helvetica"</c>.</summary>
    [JsonPropertyName("replacementFont")]
    public string ReplacementFont { get; set; } = "helvetica";

    /// <summary>Gets or sets the maximum font size for replacement text. Defaults to 12.</summary>
    [JsonPropertyName("replacementMaxFontSize")]
    public float ReplacementMaxFontSize { get; set; } = 12;

    /// <summary>Gets or sets the color of the replacement text, or <see langword="null" /> for the default.</summary>
    [JsonPropertyName("replacementFontColor")]
    public string? ReplacementFontColor { get; set; }

    /// <summary>Gets or sets the rendering scale. Defaults to 0.25.</summary>
    [JsonPropertyName("scale")]
    public float Scale { get; set; } = 0.25f;

    /// <summary>Gets or sets the rendering DPI. Defaults to 150.</summary>
    [JsonPropertyName("dpi")]
    public int Dpi { get; set; } = 150;

    /// <summary>Gets or sets the output image compression quality. Defaults to 1.0.</summary>
    [JsonPropertyName("compressionQuality")]
    public float CompressionQuality { get; set; } = 1.0f;

    /// <summary>Gets or sets a value indicating whether pages with no redactions are preserved as-is. Defaults to <see langword="false" />.</summary>
    [JsonPropertyName("preserveUnredactedPages")]
    public bool PreserveUnredactedPages { get; set; } = false;
}
