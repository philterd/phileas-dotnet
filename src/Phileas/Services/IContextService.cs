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
}