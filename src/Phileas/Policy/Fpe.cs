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
/// Format-preserving encryption (FPE) settings used by the <c>FPE_ENCRYPT_REPLACE</c>
/// filter strategy.
/// </summary>
public class Fpe
{
    /// <summary>Gets or sets the Base64-encoded encryption key.</summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>Gets or sets the Base64-encoded tweak value used to customize the encryption.</summary>
    [JsonPropertyName("tweak")]
    public string? Tweak { get; set; }
}
