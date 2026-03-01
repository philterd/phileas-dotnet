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

public class PostFilters
{
    [JsonPropertyName("trailingNewLines")] public bool TrailingNewLines { get; set; } = true;

    [JsonPropertyName("trailingPeriods")] public bool TrailingPeriods { get; set; } = true;

    [JsonPropertyName("trailingSpaces")] public bool TrailingSpaces { get; set; } = true;
}