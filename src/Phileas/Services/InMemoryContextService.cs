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

namespace Phileas.Services;

/// <summary>
///     Default in-memory implementation of <see cref="IContextService" />.
///     Stores PII token → replacement value mappings in a thread-safe, in-memory dictionary
///     keyed by context name.
/// </summary>
public class InMemoryContextService : IContextService
{
    private readonly Dictionary<string, Dictionary<string, string>> _contexts = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public string? Get(string contextName, string token)
    {
        lock (_lock)
        {
            if (_contexts.TryGetValue(contextName, out var ctx) &&
                ctx.TryGetValue(token, out var value))
                return value;
            return null;
        }
    }

    /// <inheritdoc />
    public void Put(string contextName, string token, string replacement)
    {
        lock (_lock)
        {
            if (!_contexts.TryGetValue(contextName, out var ctx))
            {
                ctx = new Dictionary<string, string>();
                _contexts[contextName] = ctx;
            }

            ctx[token] = replacement;
        }
    }
}