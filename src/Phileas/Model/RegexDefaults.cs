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
///     Defaults for regular expressions built outside a filter, where no
///     <see cref="Phileas.Filters.FilterConfiguration" /> is in scope to supply a per-policy budget.
/// </summary>
public static class RegexDefaults
{
    /// <summary>
    ///     Budget for a single match. Every pattern in this library is constructed with a budget: an
    ///     unbounded one lets hostile input stall filtering indefinitely, and .NET's default is
    ///     unbounded. Matches the default of
    ///     <see cref="Phileas.Filters.FilterConfiguration.RegexTimeoutMs" />.
    /// </summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(1000);
}
