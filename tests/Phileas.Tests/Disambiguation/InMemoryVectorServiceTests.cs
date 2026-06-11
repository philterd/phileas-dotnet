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
using Phileas.Services.Disambiguation.Vector;
using Xunit;

namespace Phileas.Tests.Disambiguation;

public class InMemoryVectorServiceTests
{
    private static Span Span(FilterType filterType, string context)
    {
        return Model.Span.Make(0, 4, filterType, context, 0.0, "x", "x", "", false, true, new[] { "x" }, 0);
    }

    [Fact]
    public void ConcurrentInsertsDoNotLoseCounts()
    {
        // Documents are processed concurrently, so concurrent inserts into the same context must not lose
        // increments. A check-then-act increment would drop counts under contention.
        var vectorService = new InMemoryVectorService();

        const int vectorSize = 16;
        const string context = "c";
        const int threads = 8;
        const int insertsPerThread = 5_000;
        const int index = 3;

        var hashes = new double[vectorSize];
        hashes[index] = 1;
        var span = Span(FilterType.Ssn, context);

        Parallel.For(0, threads, _ =>
        {
            for (var i = 0; i < insertsPerThread; i++)
                vectorService.HashAndInsert(context, hashes, span, vectorSize);
        });

        var count = vectorService.GetVectorRepresentation(context, FilterType.Ssn)[index];
        Assert.Equal((double)threads * insertsPerThread, count);
    }

    [Fact]
    public void VectorsAreIsolatedPerContext()
    {
        var vectorService = new InMemoryVectorService();

        var hashes = new double[16];
        hashes[2] = 1;

        vectorService.HashAndInsert("a", hashes, Span(FilterType.Ssn, "a"), 16);

        Assert.Equal(1.0, vectorService.GetVectorRepresentation("a", FilterType.Ssn)[2.0]);
        Assert.True(vectorService.GetVectorRepresentation("b", FilterType.Ssn).IsEmpty);
    }
}
