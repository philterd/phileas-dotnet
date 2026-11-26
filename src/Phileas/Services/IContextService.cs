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

using Phileas.Model;

namespace Phileas.Services;

/// <summary>
///     Provides referential integrity for the RANDOM_REPLACE filter strategy by
///     persisting PII token → replacement value mappings within named contexts.
/// </summary>
public interface IContextService
{
    /// <summary>
    ///     Returns the replacement value previously stored for the given token in the
    ///     specified context, or <see langword="null" /> if no value has been stored yet.
    /// </summary>
    string? Get(string contextName, string token);

    /// <summary>
    ///     Stores a replacement value for the given token in the specified context.
    /// </summary>
    void Put(string contextName, string token, string replacement);

    /// <summary>
    ///     Returns a value that belongs to the context rather than to a token, creating and storing one with
    ///     <paramref name="valueFactory" /> only if the context has no value for <paramref name="key" /> yet.
    ///     The date filter keeps a context's random date shift this way.
    ///     <para>
    ///         The default implementation stores the value as a token entry, with <paramref name="key" /> as the
    ///         token, through <see cref="Get" /> and <see cref="Put" />. It is not atomic: two calls that both find
    ///         no value can each create one, and the one stored last is kept. <see cref="InMemoryContextService" />
    ///         overrides it to create the value once. An implementation shared between threads or processes should
    ///         do the same, and one that evicts, deletes, or lists a context's entries should keep the value with
    ///         the context itself, so it lasts as long as the context does.
    ///     </para>
    /// </summary>
    /// <param name="contextName">The context the value belongs to.</param>
    /// <param name="key">
    ///     The name of the value. The default implementation keeps it among the tokens, so use a key no
    ///     detected token can be, such as one containing a NUL character.
    /// </param>
    /// <param name="filterType">The filter type the value is kept for. The default implementation does not store it.</param>
    /// <param name="valueFactory">Creates the value when the context has none for the key.</param>
    /// <returns>The stored value, or the one just created.</returns>
    string ComputeValueIfAbsent(string contextName, string key, FilterType filterType, Func<string> valueFactory)
    {
        var existing = Get(contextName, key);
        if (existing != null)
            return existing;

        var value = valueFactory();
        Put(contextName, key, value);
        return value;
    }
}