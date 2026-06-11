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

using System.Collections.Concurrent;
using Phileas.Model;

namespace Phileas.Services.Disambiguation.Vector;

/// <summary>
///     Stores and retrieves the accumulated disambiguation vectors, keyed by context and filter type.
/// </summary>
public interface IVectorService
{
    /// <summary>
    ///     Accumulates the set indexes of <paramref name="hashes" /> into the vector for the span's filter
    ///     type within <paramref name="context" />.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="hashes">The per-index presence vector for the span's context window (non-zero = present).</param>
    /// <param name="span">The span whose filter type the vector accumulates under.</param>
    /// <param name="vectorSize">The vector size.</param>
    void HashAndInsert(string context, double[] hashes, Span span, int vectorSize);

    /// <summary>
    ///     Returns the accumulated sparse vector (index→count) for <paramref name="filterType" /> within
    ///     <paramref name="context" />.
    /// </summary>
    ConcurrentDictionary<double, double> GetVectorRepresentation(string context, FilterType filterType);
}
