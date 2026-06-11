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
///     A <see cref="IVectorService" /> that stores everything in memory.
///     <para>
///         Documents are routinely processed concurrently, so all mutation here uses the atomic
///         primitives of <see cref="ConcurrentDictionary{TKey,TValue}" /> (<c>GetOrAdd</c>,
///         <c>AddOrUpdate</c>) rather than check-then-act sequences, which would otherwise lose counts
///         under contention.
///     </para>
/// </summary>
public class InMemoryVectorService : IVectorService
{
    /// <summary>The context → (filter type → vector) cache.</summary>
    protected readonly ConcurrentDictionary<string, Dictionary<FilterType, SpanVector>> VectorCache = new();

    /// <inheritdoc />
    public void HashAndInsert(string context, double[] hashes, Span span, int vectorSize)
    {
        var vectorIndexes = InitializeVectorCache(context)[span.FilterType].VectorIndexes;

        for (var i = 0; i < hashes.Length; i++)
        {
            if (hashes[i] != 0)
            {
                // Atomically accumulate the count for this index. We only care that the token was present
                // in the window; the magnitude is the number of windows that hit this index.
                vectorIndexes.AddOrUpdate(i, 1.0, (_, existing) => existing + 1.0);
            }
        }
    }

    /// <inheritdoc />
    public ConcurrentDictionary<double, double> GetVectorRepresentation(string context, FilterType filterType)
    {
        return InitializeVectorCache(context)[filterType].VectorIndexes;
    }

    /// <summary>
    ///     Returns the per-filter-type vectors for the context, creating them atomically on first use. The
    ///     map is fully populated for every <see cref="FilterType" /> before it is published, so callers
    ///     never see a partially-initialized context.
    /// </summary>
    private Dictionary<FilterType, SpanVector> InitializeVectorCache(string context)
    {
        return VectorCache.GetOrAdd(context, _ =>
        {
            var vector = new Dictionary<FilterType, SpanVector>();
            foreach (FilterType filterType in Enum.GetValues<FilterType>())
                vector[filterType] = new SpanVector();
            return vector;
        });
    }
}
