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
///     Format-preserving encryption (FPE) settings used by the <c>FPE_ENCRYPT_REPLACE</c>
///     filter strategy.
/// </summary>
public class Fpe
{
    /// <summary>Creates an empty <see cref="Fpe" /> (used by JSON deserialization).</summary>
    public Fpe()
    {
    }

    /// <summary>Creates an <see cref="Fpe" /> with the given key and tweak.</summary>
    /// <param name="key">The Base64-encoded key, or an <c>env:NAME</c> reference.</param>
    /// <param name="tweak">The Base64-encoded tweak, or an <c>env:NAME</c> reference.</param>
    public Fpe(string? key, string? tweak)
    {
        Key = key;
        Tweak = tweak;
    }

    /// <summary>Gets or sets the Base64-encoded encryption key, or an <c>env:NAME</c> reference.</summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>Gets or sets the Base64-encoded tweak value used to customize the encryption, or an <c>env:NAME</c> reference.</summary>
    [JsonPropertyName("tweak")]
    public string? Tweak { get; set; }

    /// <summary>
    ///     Returns the key, resolving an <c>env:NAME</c> reference to the value of the named environment
    ///     variable. PhiSQL-authored policies store keys as <c>env:NAME</c> so secrets stay out of the policy.
    /// </summary>
    public string? GetKey()
    {
        return EnvResolver.Resolve(Key);
    }

    /// <summary>Returns the tweak, resolving an <c>env:NAME</c> reference to the named environment variable's value.</summary>
    public string? GetTweak()
    {
        return EnvResolver.Resolve(Tweak);
    }
}