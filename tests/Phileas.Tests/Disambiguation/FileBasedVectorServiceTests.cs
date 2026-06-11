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
using Phileas.Services.Disambiguation;
using Phileas.Services.Disambiguation.Vector;
using Xunit;

namespace Phileas.Tests.Disambiguation;

public class FileBasedVectorServiceTests : IDisposable
{
    private const int VectorSize = 64;
    private const string Algorithm = "murmur3";

    private readonly string _dir = Path.Combine(Path.GetTempPath(),
        "phileas-vec-" + Guid.NewGuid().ToString("N"));

    public FileBasedVectorServiceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private static VectorBasedSpanDisambiguationService Service(IVectorService vectorService)
    {
        var options = new SpanDisambiguationOptions
            { Enabled = true, IgnoreStopWords = false, VectorSize = VectorSize };
        return new VectorBasedSpanDisambiguationService(options, vectorService);
    }

    private static Span PhoneSpan(string context)
    {
        return Span.Make(0, 4, FilterType.PhoneNumber, context, 0.0, "555-1212", "x", "", false, true,
            new[] { "phone", "number", "call" }, 0);
    }

    private static double[] Single(int index, int size)
    {
        var hashes = new double[size];
        hashes[index] = 1;
        return hashes;
    }

    private static Span SsnSpan()
    {
        return Span.Make(0, 4, FilterType.Ssn, "c", 0.0, "x", "x", "", false, true, new[] { "x" }, 0);
    }

    [Fact]
    public void MissingFileIsAColdStart()
    {
        var path = Path.Combine(_dir, "does-not-exist.json");
        var vectorService = new FileBasedVectorService(path, VectorSize, Algorithm);

        Assert.True(vectorService.GetVectorRepresentation("c", FilterType.Ssn).IsEmpty);
    }

    [Fact]
    public void VectorsSurviveSaveAndReload()
    {
        var path = Path.Combine(_dir, "vectors.json");
        const string context = "c";

        var original = new FileBasedVectorService(path, VectorSize, Algorithm);
        Service(original).HashAndInsert(context, PhoneSpan(context));

        var beforeSave = new Dictionary<double, double>(original.GetVectorRepresentation(context, FilterType.PhoneNumber));
        Assert.NotEmpty(beforeSave);
        original.Save();
        Assert.True(new FileInfo(path).Length > 0);

        var reloaded = new FileBasedVectorService(path, VectorSize, Algorithm);
        var after = reloaded.GetVectorRepresentation(context, FilterType.PhoneNumber);
        Assert.Equal(beforeSave.Count, after.Count);
        foreach (var (key, value) in beforeSave)
            Assert.Equal(value, after[key]);

        // The restored store still drives a correct decision.
        var candidates = new List<FilterType> { FilterType.Ssn, FilterType.PhoneNumber };
        var ambiguous = Span.Make(0, 4, FilterType.Ssn, context, 0.0, "123-4567", "x", "", false, true,
            new[] { "phone", "number", "call" }, 0);
        Assert.Equal(FilterType.PhoneNumber, Service(reloaded).Disambiguate(context, candidates, ambiguous));
    }

    [Fact]
    public void AccumulationContinuesAfterReload()
    {
        var path = Path.Combine(_dir, "vectors.json");
        const string context = "c";
        const int index = 5;
        var hashes = Single(index, 64);

        var original = new FileBasedVectorService(path, VectorSize, Algorithm);
        original.HashAndInsert(context, hashes, SsnSpan(), 64);
        original.HashAndInsert(context, hashes, SsnSpan(), 64);
        original.Save();

        var reloaded = new FileBasedVectorService(path, VectorSize, Algorithm);
        reloaded.HashAndInsert(context, hashes, SsnSpan(), 64);

        Assert.Equal(3.0, reloaded.GetVectorRepresentation(context, FilterType.Ssn)[index]);
    }

    [Fact]
    public void DisposeSavesTheVectors()
    {
        var path = Path.Combine(_dir, "vectors.json");
        const string context = "c";

        using (var vectorService = new FileBasedVectorService(path, VectorSize, Algorithm))
        {
            Service(vectorService).HashAndInsert(context, Span.Make(0, 4, FilterType.Ssn, context, 0.0, "x", "x", "",
                false, true, new[] { "social", "security" }, 0));
        }

        Assert.True(File.Exists(path) && new FileInfo(path).Length > 0);
        Assert.False(new FileBasedVectorService(path, VectorSize, Algorithm)
            .GetVectorRepresentation(context, FilterType.Ssn).IsEmpty);
    }

    [Fact]
    public void VectorsBuiltWithADifferentVectorSizeAreDiscarded()
    {
        var path = Path.Combine(_dir, "vectors.json");
        const string context = "c";

        var original = new FileBasedVectorService(path, 64, Algorithm);
        original.HashAndInsert(context, Single(5, 64), SsnSpan(), 64);
        original.Save();

        var reloaded = new FileBasedVectorService(path, 128, Algorithm);
        Assert.True(reloaded.GetVectorRepresentation(context, FilterType.Ssn).IsEmpty);
    }

    [Fact]
    public void VectorsBuiltWithADifferentHashAlgorithmAreDiscarded()
    {
        var path = Path.Combine(_dir, "vectors.json");
        const string context = "c";

        var original = new FileBasedVectorService(path, VectorSize, "murmur3");
        original.HashAndInsert(context, Single(5, VectorSize), SsnSpan(), VectorSize);
        original.Save();

        var reloaded = new FileBasedVectorService(path, VectorSize, "hashCode");
        Assert.True(reloaded.GetVectorRepresentation(context, FilterType.Ssn).IsEmpty);
    }

    [Fact]
    public void EquivalentHashAlgorithmNamesAreNotTreatedAsAMismatch()
    {
        var path = Path.Combine(_dir, "vectors.json");
        const string context = "c";

        var original = new FileBasedVectorService(path, VectorSize, "hashCode");
        original.HashAndInsert(context, Single(5, VectorSize), SsnSpan(), VectorSize);
        original.Save();

        // Anything that is not "murmur3" means the string-hash fallback, so two such names are the same.
        var reloaded = new FileBasedVectorService(path, VectorSize, "java");
        Assert.False(reloaded.GetVectorRepresentation(context, FilterType.Ssn).IsEmpty);
    }
}
