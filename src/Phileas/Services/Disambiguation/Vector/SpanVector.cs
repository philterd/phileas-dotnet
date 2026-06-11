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

namespace Phileas.Services.Disambiguation.Vector;

/// <summary>
///     A sparse accumulated vector for one filter type within one context: a map from vector index to
///     the number of training windows that hit that index. Backed by a concurrent map so accumulation is
///     thread-safe under the concurrent document processing the pipeline performs.
/// </summary>
public class SpanVector
{
    /// <summary>Creates an empty span vector.</summary>
    public SpanVector()
    {
        VectorIndexes = new ConcurrentDictionary<double, double>();
    }

    /// <summary>Gets or sets the sparse index→count map for this vector.</summary>
    public ConcurrentDictionary<double, double> VectorIndexes { get; set; }
}
