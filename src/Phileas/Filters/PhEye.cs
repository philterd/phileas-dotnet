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
using Phileas.Filters;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class PhEye : AbstractPolicyFilter
{
    [JsonPropertyName("phEyeFilterStrategies")]
    public List<PhEyeFilterStrategy>? Strategies { get; set; }

    [JsonPropertyName("phEyeConfiguration")]
    public PhEyeConfiguration PhEyeConfiguration { get; set; } = new PhEyeConfiguration();

    [JsonPropertyName("removePunctuation")]
    public bool RemovePunctuation { get; set; } = false;

    [JsonPropertyName("thresholds")]
    public Dictionary<string, double> Thresholds { get; set; } = new Dictionary<string, double>();
}
