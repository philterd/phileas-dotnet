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
///     Graphical redaction configuration: a list of fixed bounding boxes to redact in a document.
/// </summary>
public class Graphical
{
    /// <summary>Gets or sets the list of fixed bounding boxes to redact.</summary>
    [JsonPropertyName("boundingBoxes")]
    public List<BoundingBox> BoundingBoxes { get; set; } = new();
}
