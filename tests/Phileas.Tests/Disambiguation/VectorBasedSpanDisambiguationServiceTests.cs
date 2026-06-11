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

using System.Text;
using Phileas.Model;
using Phileas.Services.Disambiguation;
using Phileas.Services.Disambiguation.Vector;
using Phileas.Utils;
using Xunit;

namespace Phileas.Tests.Disambiguation;

public class VectorBasedSpanDisambiguationServiceTests
{
    private static VectorBasedSpanDisambiguationService Service(
        IVectorService vectorService, int vectorSize = 32, bool ignoreStopWords = false,
        string hashAlgorithm = "murmur3", string? stopWords = null)
    {
        var options = new SpanDisambiguationOptions
        {
            Enabled = true,
            VectorSize = vectorSize,
            IgnoreStopWords = ignoreStopWords,
            HashAlgorithm = hashAlgorithm
        };
        if (stopWords != null) options.StopWords = stopWords;
        return new VectorBasedSpanDisambiguationService(options, vectorService);
    }

    private static Span MakeSpan(FilterType filterType, string context, double confidence, string[] window)
    {
        return Model.Span.Make(0, 4, filterType, context, confidence, "123-45-6789", "000-00-0000", "",
            false, true, window, 0);
    }

    [Fact]
    public void DisambiguateLocal1()
    {
        var vectorService = new InMemoryVectorService();
        const string context = "c";
        var service = Service(vectorService);

        service.HashAndInsert(context, MakeSpan(FilterType.Ssn, context, 0.0, new[] { "ssn", "was", "he", "id" }));
        service.HashAndInsert(context, MakeSpan(FilterType.Ssn, context, 0.0, new[] { "ssn", "asdf", "he", "was" }));
        service.HashAndInsert(context, MakeSpan(FilterType.PhoneNumber, context, 0.0, new[] { "phone", "number", "she", "had" }));

        var filterTypes = new List<FilterType> { FilterType.Ssn, FilterType.PhoneNumber };
        var ambiguousSpan = MakeSpan(FilterType.PhoneNumber, context, 0.0, new[] { "phone", "number", "called", "is" });

        Assert.Equal(FilterType.PhoneNumber, service.Disambiguate(context, filterTypes, ambiguousSpan));
    }

    [Fact]
    public void DisambiguateLocal2()
    {
        var vectorService = new InMemoryVectorService();
        const string context = "c";
        var service = Service(vectorService);

        var span = MakeSpan(FilterType.Ssn, context, 0.0, new[] { "ssn", "asdf", "he", "was" });
        var span1 = MakeSpan(FilterType.Ssn, context, 0.0, new[] { "ssn", "was", "he", "id" });
        var span2 = MakeSpan(FilterType.PhoneNumber, context, 0.0, new[] { "phone", "number", "she", "had" });
        var ambiguousSpan = MakeSpan(FilterType.PhoneNumber, context, 0.0, new[] { "phone", "number", "called", "is" });

        var disambiguated = service.Disambiguate(context, new List<Span> { span, span1, span2, ambiguousSpan });

        // Each competing span resolves to the type its own window supports; after resolution the duplicate
        // per-type spans at the same location dedupe, leaving one SSN and one PHONE_NUMBER.
        var resolvedTypes = disambiguated.Select(s => s.FilterType).ToHashSet();
        Assert.Equal(2, disambiguated.Count);
        Assert.Contains(FilterType.Ssn, resolvedTypes);
        Assert.Contains(FilterType.PhoneNumber, resolvedTypes);
    }

    [Fact]
    public void StopWordsAreParsedAndRemovedFromTheVector()
    {
        // A window made up entirely of (multi-word, comma-separated, mixed-case) stop words contributes
        // nothing when stop word handling is on. Mixed case proves the match is case-insensitive.
        var withStopWords = new InMemoryVectorService();
        var ignoring = Service(withStopWords, ignoreStopWords: true, stopWords: "alpha, beta");
        ignoring.HashAndInsert("c", Model.Span.Make(0, 4, FilterType.Ssn, "c", 0.0, "x", "x", "",
            false, true, new[] { "Alpha", "BETA" }, 0));
        Assert.True(withStopWords.GetVectorRepresentation("c", FilterType.Ssn).IsEmpty);

        // With stop word handling off, the very same window does contribute.
        var noStopWords = new InMemoryVectorService();
        var keeping = Service(noStopWords, ignoreStopWords: false, stopWords: "alpha, beta");
        keeping.HashAndInsert("c", Model.Span.Make(0, 4, FilterType.Ssn, "c", 0.0, "x", "x", "",
            false, true, new[] { "Alpha", "BETA" }, 0));
        Assert.False(noStopWords.GetVectorRepresentation("c", FilterType.Ssn).IsEmpty);
    }

    [Fact]
    public void HashAlgorithmSelectionUsesTheConfiguredAlgorithm()
    {
        var vectorService = new InMemoryVectorService();
        var defaultAlgo = Service(vectorService);
        var murmur3 = Service(vectorService, hashAlgorithm: "murmur3");
        var hashCode = Service(vectorService, hashAlgorithm: "hashCode");

        var tokens = new[] { "phone", "number", "ssn", "called", "office", "social", "security" };

        var murmurDiffersFromHashCode = false;
        foreach (var token in tokens)
        {
            Assert.Equal(murmur3.HashToken(token), defaultAlgo.HashToken(token));
            if (murmur3.HashToken(token) != hashCode.HashToken(token))
                murmurDiffersFromHashCode = true;
        }

        Assert.True(murmurDiffersFromHashCode);
    }

    [Fact]
    public void Murmur3HashesUtf8BytesForCrossPlatformDeterminism()
    {
        const int vectorSize = 512;
        var service = Service(new InMemoryVectorService(), vectorSize, hashAlgorithm: "murmur3");

        const string token = "naïve";
        var expected = Math.Abs(MurmurHash3.Hash32X86(Encoding.UTF8.GetBytes(token)) % vectorSize);

        Assert.Equal(expected, service.HashToken(token));
    }

    [Fact]
    public void HashingIsCaseInsensitive()
    {
        // Train PHONE_NUMBER on lower-cased phone context, then disambiguate an ambiguous span whose window
        // is the same words but capitalized. A large vector size ensures a case mismatch genuinely produces
        // no overlap rather than a chance collision.
        var service = Service(new InMemoryVectorService(), 4096, ignoreStopWords: false);
        const string context = "c";

        service.HashAndInsert(context, Model.Span.Make(0, 4, FilterType.PhoneNumber, context, 0.0, "555-1212", "x", "",
            false, true, new[] { "phone", "number", "call" }, 0));

        var candidates = new List<FilterType> { FilterType.Ssn, FilterType.PhoneNumber };
        var ambiguousSpan = Model.Span.Make(0, 4, FilterType.Ssn, context, 0.0, "123-4567", "x", "",
            false, true, new[] { "Phone", "Number", "Call" }, 0);

        Assert.Equal(FilterType.PhoneNumber, service.Disambiguate(context, candidates, ambiguousSpan));
    }

    [Fact]
    public void CompetingSpansSharingAWindowResolveToTheTrainedTypeAndDedupe()
    {
        var service = Service(new InMemoryVectorService());
        const string context = "c";
        var window = new[] { "phone", "number", "call", "office" };

        service.HashAndInsert(context, Model.Span.Make(0, 4, FilterType.PhoneNumber, context, 0.0, "555-1212", "x", "",
            false, true, window, 0));

        // Two competing spans at the same location with the SAME window differing only by filter type.
        var asSsn = Model.Span.Make(0, 4, FilterType.Ssn, context, 0.5, "123-45-6789", "x", "", false, true, window, 0);
        var asPhone = Model.Span.Make(0, 4, FilterType.PhoneNumber, context, 0.5, "123-45-6789", "x", "", false, true, window, 0);

        var resolved = service.Disambiguate(context, new List<Span> { asSsn, asPhone });

        Assert.Single(resolved);
        Assert.Equal(FilterType.PhoneNumber, resolved[0].FilterType);
    }

    [Fact]
    public void AmbiguousSpansAreNotRecordedAsTrainingData()
    {
        var vectorService = new InMemoryVectorService();
        var service = Service(vectorService);
        const string context = "c";
        var window = new[] { "phone", "number", "call", "office" };

        service.HashAndInsert(context, Model.Span.Make(0, 4, FilterType.PhoneNumber, context, 0.0, "555-1212", "x", "",
            false, true, window, 0));

        var phoneBefore = new Dictionary<double, double>(vectorService.GetVectorRepresentation(context, FilterType.PhoneNumber));
        var ssnBefore = new Dictionary<double, double>(vectorService.GetVectorRepresentation(context, FilterType.Ssn));

        var asSsn = Model.Span.Make(0, 4, FilterType.Ssn, context, 0.5, "123456789", "x", "", false, true, window, 0);
        var asPhone = Model.Span.Make(0, 4, FilterType.PhoneNumber, context, 0.5, "123456789", "x", "", false, true, window, 0);

        service.Disambiguate(context, new List<Span> { asSsn, asPhone });

        Assert.Equal(phoneBefore, vectorService.GetVectorRepresentation(context, FilterType.PhoneNumber));
        Assert.Equal(ssnBefore, vectorService.GetVectorRepresentation(context, FilterType.Ssn));
    }

    [Fact]
    public void TrainingIsIsolatedPerContext()
    {
        var service = Service(new InMemoryVectorService());

        service.HashAndInsert("a", Model.Span.Make(0, 4, FilterType.PhoneNumber, "a", 0.0, "555-1212", "x", "",
            false, true, new[] { "phone", "number", "call" }, 0));

        var candidates = new List<FilterType> { FilterType.Ssn, FilterType.PhoneNumber };
        var ambiguous = Model.Span.Make(0, 4, FilterType.Ssn, "a", 0.0, "123-4567", "x", "",
            false, true, new[] { "phone", "number", "call" }, 0);

        Assert.Equal(FilterType.PhoneNumber, service.Disambiguate("a", candidates, ambiguous));
        Assert.Equal(FilterType.Ssn, service.Disambiguate("b", candidates, ambiguous));
    }

    [Fact]
    public void AccumulatedCountsWeightTheDecision()
    {
        var service = Service(new InMemoryVectorService(), 512, ignoreStopWords: false);
        const string context = "c";

        Assert.NotEqual(service.HashToken("alpha"), service.HashToken("beta"));

        // SSN becomes alpha-heavy: {alpha:3, beta:1}.
        service.HashAndInsert(context, MakeSpan(FilterType.Ssn, "alpha", "beta"));
        service.HashAndInsert(context, MakeSpan(FilterType.Ssn, "alpha"));
        service.HashAndInsert(context, MakeSpan(FilterType.Ssn, "alpha"));

        // PHONE_NUMBER becomes beta-heavy: {alpha:1, beta:3}.
        service.HashAndInsert(context, MakeSpan(FilterType.PhoneNumber, "alpha", "beta"));
        service.HashAndInsert(context, MakeSpan(FilterType.PhoneNumber, "beta"));
        service.HashAndInsert(context, MakeSpan(FilterType.PhoneNumber, "beta"));

        // Ambiguous span contains only "alpha", which SSN accumulated more often. List PHONE_NUMBER first so
        // an SSN win cannot be a cold-start/ordering artifact.
        var candidates = new List<FilterType> { FilterType.PhoneNumber, FilterType.Ssn };
        Assert.Equal(FilterType.Ssn, service.Disambiguate(context, candidates, MakeSpan(FilterType.Ssn, "alpha")));
    }

    private static Span MakeSpan(FilterType filterType, params string[] window)
    {
        return Model.Span.Make(0, 4, filterType, "c", 0.0, "x", "x", "", false, true, window, 0);
    }

    [Fact]
    public void CosineSimilarityHandlesIdenticalOrthogonalAndZeroVectors()
    {
        // Identical direction -> 1.0 regardless of magnitude.
        Assert.Equal(1.0,
            VectorBasedSpanDisambiguationService.CosineSimilarity(new double[] { 3, 0, 0 }, new double[] { 1, 0, 0 }), 9);

        // Orthogonal -> 0.0.
        Assert.Equal(0.0,
            VectorBasedSpanDisambiguationService.CosineSimilarity(new double[] { 1, 0 }, new double[] { 0, 1 }), 9);

        // A zero vector has no direction -> NaN; disambiguate() relies on this (NaN -> 0).
        Assert.True(double.IsNaN(
            VectorBasedSpanDisambiguationService.CosineSimilarity(new double[] { 0, 0 }, new double[] { 1, 1 })));
    }

    [Fact]
    public void ColdStartIsDeterministicAndReturnsACandidate()
    {
        var service = Service(new InMemoryVectorService());

        var ambiguousSpan = Model.Span.Make(0, 4, FilterType.Ssn, "c", 0.0, "123-45-6789", "x", "",
            false, true, new[] { "some", "unseen", "words" }, 0);
        var candidates = new List<FilterType> { FilterType.Ssn, FilterType.PhoneNumber };

        var first = service.Disambiguate("c", candidates, ambiguousSpan);
        var second = service.Disambiguate("c", candidates, ambiguousSpan);

        Assert.Equal(FilterType.Ssn, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void TrainingImprovesTheDecision()
    {
        var service = Service(new InMemoryVectorService());
        const string context = "c";

        service.HashAndInsert(context, Model.Span.Make(0, 4, FilterType.PhoneNumber, context, 0.0, "555-1212", "x", "",
            false, true, new[] { "phone", "number", "call", "reached" }, 0));
        service.HashAndInsert(context, Model.Span.Make(0, 4, FilterType.PhoneNumber, context, 0.0, "555-3434", "x", "",
            false, true, new[] { "phone", "number", "dial", "office" }, 0));

        var candidates = new List<FilterType> { FilterType.Ssn, FilterType.PhoneNumber };
        var ambiguousSpan = Model.Span.Make(0, 4, FilterType.Ssn, context, 0.0, "123-4567", "x", "",
            false, true, new[] { "phone", "number", "call", "office" }, 0);

        Assert.Equal(FilterType.PhoneNumber, service.Disambiguate(context, candidates, ambiguousSpan));
    }

    [Fact]
    public void UnambiguousSpansAreUsedAsTrainingData()
    {
        var vectorService = new InMemoryVectorService();
        var service = Service(vectorService);
        const string context = "c";

        var phone = Model.Span.Make(0, 4, FilterType.PhoneNumber, context, 0.0, "555-1212", "x", "",
            false, true, new[] { "phone", "number", "call" }, 0);

        service.Disambiguate(context, new List<Span> { phone });

        Assert.False(vectorService.GetVectorRepresentation(context, FilterType.PhoneNumber).IsEmpty);
    }
}
