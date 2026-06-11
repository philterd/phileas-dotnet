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
///     AES-256 encryption key and initialization vector used by the <c>CRYPTO_REPLACE</c>
///     filter strategy. Both values must be Base64-encoded.
/// </summary>
public class Crypto
{
    /// <summary>Creates an empty <see cref="Crypto" /> (used by JSON deserialization).</summary>
    public Crypto()
    {
    }

    /// <summary>Creates a <see cref="Crypto" /> with the given key and IV.</summary>
    /// <param name="key">The Base64-encoded key, or an <c>env:NAME</c> reference.</param>
    /// <param name="iv">The Base64-encoded IV, or an <c>env:NAME</c> reference.</param>
    public Crypto(string? key, string? iv)
    {
        Key = key;
        Iv = iv;
    }

    /// <summary>Gets or sets the Base64-encoded AES-256 encryption key, or an <c>env:NAME</c> reference.</summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>Gets or sets the Base64-encoded AES initialization vector (IV), or an <c>env:NAME</c> reference.</summary>
    [JsonPropertyName("iv")]
    public string? Iv { get; set; }

    /// <summary>
    ///     Returns the key, resolving an <c>env:NAME</c> reference to the value of the named environment
    ///     variable. PhiSQL-authored policies store keys as <c>env:NAME</c> so secrets stay out of the policy.
    /// </summary>
    public string? GetKey()
    {
        return EnvResolver.Resolve(Key);
    }

    /// <summary>Returns the IV, resolving an <c>env:NAME</c> reference to the named environment variable's value.</summary>
    public string? GetIv()
    {
        return EnvResolver.Resolve(Iv);
    }
}