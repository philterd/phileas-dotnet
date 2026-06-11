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

namespace Phileas.Services.Disambiguation.Vector;

/// <summary>
///     A <see cref="ISpanDisambiguationService" /> that uses accumulated context vectors and cosine
///     similarity to determine which filter type an ambiguous span is most similar to.
/// </summary>
public class VectorBasedSpanDisambiguationService : AbstractSpanDisambiguationService, ISpanDisambiguationService
{
    /// <summary>Initializes the service from the given options and vector store.</summary>
    public VectorBasedSpanDisambiguationService(SpanDisambiguationOptions options, IVectorService vectorService)
        : base(options, vectorService)
    {
    }

    /// <inheritdoc />
    public void HashAndInsert(string context, Span span)
    {
        var hashes = Hash(span);
        VectorService.HashAndInsert(context, hashes, span, VectorSize);
    }

    /// <inheritdoc />
    public IList<Span> Disambiguate(string context, IList<Span> spans)
    {
        // A set with Java-equivalent value equality preserves insertion order and dedupes spans that
        // become identical once their filter type is resolved.
        var disambiguatedSpans = new List<Span>();
        var seen = new HashSet<Span>(SpanValueEqualityComparer.Instance);

        // Determine each span's competitors up front, against the original (unmutated) list. The loop below
        // resolves spans by changing their filter type in place; if competitors were recomputed mid-loop,
        // resolving one span to match its rival would make that rival look unambiguous and it would be
        // wrongly recorded as training data. Reference equality keys keep per-object lists even if two
        // spans become equal once their filter types match after resolution.
        var competingSpansBySpan = new Dictionary<Span, List<Span>>(ReferenceEqualityComparer.Instance);
        foreach (var span in spans)
            competingSpansBySpan[span] = GetCompetingSpans(span, spans);

        foreach (var span in spans)
        {
            // The spans that compete with this one (same location, different filter type), as computed
            // before any resolution mutated the list.
            var identicalSpans = competingSpansBySpan[span];

            if (identicalSpans.Count > 0)
            {
                // Candidate filter types: this span's own type plus the competing types. The span's own
                // type must be included or it could never win the disambiguation.
                var candidateTypes = new List<FilterType> { span.FilterType };
                foreach (var competitor in identicalSpans)
                    if (!candidateTypes.Contains(competitor.FilterType))
                        candidateTypes.Add(competitor.FilterType);

                // The "ambiguous span" is any of the spans in the list since they only differ by filter type.
                var disambiguatedFilterType = Disambiguate(context, candidateTypes, span);

                span.FilterType = disambiguatedFilterType;

                if (seen.Add(span))
                    disambiguatedSpans.Add(span);
            }
            else
            {
                // This span is unambiguous: exactly one filter type claimed this text. That makes it a
                // confident training example, so record its context vector under its filter type. This is
                // what lets disambiguation improve over time.
                HashAndInsert(context, span);

                if (seen.Add(span))
                    disambiguatedSpans.Add(span);
            }
        }

        return disambiguatedSpans;
    }

    /// <inheritdoc />
    public FilterType Disambiguate(string context, IList<FilterType> filterTypes, Span ambiguousSpan)
    {
        // Build the vector for the ambiguous span from its surrounding context window.
        var ambiguousSpanVector = Hash(ambiguousSpan);

        FilterType? bestFilterType = null;
        var bestSimilarity = double.NegativeInfinity;

        foreach (var filterType in filterTypes)
        {
            // Reconstruct the accumulated vector for this filter type in this context.
            var vectorRepresentation = VectorService.GetVectorRepresentation(context, filterType);

            var filterTypeVector = new double[VectorSize];
            foreach (var (index, count) in vectorRepresentation)
                filterTypeVector[(int)index] = count;

            // Cosine similarity between the candidate's learned vector and the ambiguous span's vector. A
            // NaN result (one side had no signal, e.g. cold start or no token overlap) is treated as zero
            // so it never outranks a candidate with real overlap.
            var similarity = CosineSimilarity(filterTypeVector, ambiguousSpanVector);
            var score = double.IsNaN(similarity) ? 0.0 : similarity;

            // Strictly-greater keeps the first candidate on ties, so the decision is deterministic for a
            // given candidate ordering even before any training has accumulated.
            if (score > bestSimilarity)
            {
                bestSimilarity = score;
                bestFilterType = filterType;
            }
        }

        // Cold start / no signal: fall back to the first candidate so the result is deterministic.
        return bestFilterType ?? filterTypes[0];
    }

    /// <summary>
    ///     Returns the spans that compete with the given span for disambiguation: spans covering the same
    ///     location but assigned a different filter type. Confidence is intentionally not compared, since
    ///     competing filters routinely assign different confidences to the same text and those are exactly
    ///     the cases disambiguation must resolve.
    /// </summary>
    private static List<Span> GetCompetingSpans(Span span, IList<Span> spans)
    {
        var competing = new List<Span>();

        foreach (var other in spans)
            if (!ReferenceEquals(other, span)
                && other.CharacterStart == span.CharacterStart
                && other.CharacterEnd == span.CharacterEnd
                && other.FilterType != span.FilterType)
                competing.Add(other);

        return competing;
    }

    private double[] Hash(Span span)
    {
        var vector = new double[VectorSize];
        var window = span.Window ?? Array.Empty<string>();

        foreach (var token in window)
        {
            // Lowercase the token and remove any surrounding whitespace.
            var lowerCasedToken = token.ToLowerInvariant().Trim();

            // Ignore stop words?
            if (IgnoreStopWords && StopWords.Contains(lowerCasedToken))
                continue;

            // Hash the lower-cased token so casing does not split the signal: "Phone" at the start of a
            // sentence and "phone" mid-sentence must land on the same index. This also keeps hashing
            // consistent with the (case-insensitive) stop word check above.
            var hash = HashToken(lowerCasedToken);

            // We only care what the window contains; how many of each token is irrelevant here.
            vector[hash] = 1;
        }

        return vector;
    }

    /// <summary>Cosine similarity of two equal-length vectors. A zero-magnitude vector yields NaN.</summary>
    public static double CosineSimilarity(double[] vectorA, double[] vectorB)
    {
        var dotProduct = 0.0;
        var normA = 0.0;
        var normB = 0.0;

        for (var i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += Math.Pow(vectorA[i], 2);
            normB += Math.Pow(vectorB[i], 2);
        }

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
