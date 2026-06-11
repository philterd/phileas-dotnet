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
}
