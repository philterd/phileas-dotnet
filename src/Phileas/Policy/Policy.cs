/*
 * Copyright 2024 Philterd, LLC
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

public class Policy
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("config")]
    public Config Config { get; set; } = new Config();

    [JsonPropertyName("crypto")]
    public Crypto? Crypto { get; set; }

    [JsonPropertyName("fpe")]
    public Fpe? Fpe { get; set; }

    [JsonPropertyName("identifiers")]
    public Identifiers Identifiers { get; set; } = new Identifiers();

    [JsonPropertyName("ignored")]
    public List<Ignored> Ignored { get; set; } = new List<Ignored>();

    [JsonPropertyName("ignoredPatterns")]
    public List<IgnoredPattern> IgnoredPatterns { get; set; } = new List<IgnoredPattern>();

    [JsonPropertyName("graphical")]
    public Graphical Graphical { get; set; } = new Graphical();
}
