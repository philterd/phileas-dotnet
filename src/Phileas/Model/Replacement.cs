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

namespace Phileas.Model;

/// <summary>
///     Holds the result of applying a filter strategy to a detected entity, comprising the
///     replacement value, an optional cryptographic salt, and a flag indicating whether a
///     replacement was actually applied.
/// </summary>
public class Replacement
{
    /// <summary>
    ///     Initializes a new <see cref="Replacement" />.
    /// </summary>
    /// <param name="value">The replacement text.</param>
    /// <param name="salt">The salt used during replacement, or an empty string.</param>
    /// <param name="applied">
    ///     <see langword="true" /> if the replacement was applied; <see langword="false" /> if the original text is preserved.
    ///     Defaults to <see langword="true" />.
    /// </param>
    public Replacement(string value, string salt, bool applied = true)
    {
        Value = value;
        Salt = salt;
        Applied = applied;
    }

    /// <summary>Gets the replacement string that will be substituted for the original entity text.</summary>
    public string Value { get; }

    /// <summary>Gets the cryptographic salt that was appended before hashing, or an empty string when salting is disabled.</summary>
    public string Salt { get; }

    /// <summary>
    ///     Gets a value indicating whether a replacement was applied (<see langword="true" />) or the original text was
    ///     kept (<see langword="false" />).
    /// </summary>
    public bool Applied { get; }
}