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

using Phileas.Filters.PhEye;
using Xunit;

namespace Phileas.Tests;

/// <summary>
///     Model-free coverage of the token-aware chunking safeguard: <see cref="GlinerModel.PlanChunks" /> (keeping every
///     inference sequence within the model's <c>max_len</c>) and <see cref="GlinerModel.ResolveSpans" /> (the global
///     dedup/overlap resolution that stitches per-chunk detections back together). These run in CI with no model, the
///     same way <see cref="GlinerPipelineTests" /> covers the other deterministic stages. The end-to-end chunking run
///     against the synthetic ONNX fixture lives in <see cref="GlinerFixtureModelTests" />.
/// </summary>
public class GlinerChunkingTests
{
    private const int MaxWidth = 12;

    [Fact]
    public void PlanChunks_WithinBudget_ReturnsSingleChunk()
    {
        var counts = new[] { 3, 2, 4, 1, 2 }; // 12 word sub-tokens
        // budget = maxLen - prompt - 1 = 64 - 5 - 1 = 58, comfortably above 12.
        var chunks = GlinerModel.PlanChunks(counts, 5, 64, MaxWidth);

        var only = Assert.Single(chunks);
        Assert.Equal(0, only.Start);
        Assert.Equal(5, only.Count);
    }

    [Fact]
    public void PlanChunks_OverBudget_SplitsIntoOverlappingChunksCoveringAllWords()
    {
        var counts = Enumerable.Repeat(1, 10).ToArray(); // ten 1-token words
        // prompt 1, maxLen 6 => budget = 6 - 1 - 1 = 4 word sub-tokens per chunk; overlap maxWidth-1 = 1.
        var chunks = GlinerModel.PlanChunks(counts, 1, 6, maxWidth: 2);

        Assert.Equal(
            new[] { (0, 4), (3, 4), (6, 4) },
            chunks.Select(c => (c.Start, c.Count)).ToArray());

        // Consecutive chunks overlap by exactly maxWidth-1 = 1 word, and together they cover every word.
        for (var k = 1; k < chunks.Count; k++)
        {
            var prevEnd = chunks[k - 1].Start + chunks[k - 1].Count;
            Assert.Equal(1, prevEnd - chunks[k].Start);
        }

        var covered = chunks.SelectMany(c => Enumerable.Range(c.Start, c.Count)).Distinct();
        Assert.Equal(Enumerable.Range(0, counts.Length), covered.OrderBy(x => x));
    }

    [Fact]
    public void PlanChunks_EveryChunkSequenceStaysWithinMaxLen()
    {
        // Mixed word lengths long enough to force several chunks.
        var counts = new[] { 2, 1, 3, 1, 1, 4, 2, 1, 1, 2, 3, 1, 1, 2, 1, 1 };
        const int prompt = 6;
        const int maxLen = 20;

        var chunks = GlinerModel.PlanChunks(counts, prompt, maxLen, MaxWidth);

        foreach (var c in chunks)
        {
            var wordTokens = counts.Skip(c.Start).Take(c.Count).Sum();
            // Full sequence = prompt frame + word pieces + trailing [SEP]; it must never exceed max_len.
            Assert.True(prompt + wordTokens + 1 <= maxLen,
                $"chunk ({c.Start},{c.Count}) sequence {prompt + wordTokens + 1} exceeds max_len {maxLen}");
        }

        // Coverage is still complete despite the cap.
        var covered = chunks.SelectMany(c => Enumerable.Range(c.Start, c.Count)).Distinct().OrderBy(x => x);
        Assert.Equal(Enumerable.Range(0, counts.Length), covered);
    }

    [Fact]
    public void PlanChunks_EveryWidthSpanIsWhollyContainedInSomeChunk()
    {
        // The core "no detections lost at chunk boundaries" guarantee: for every span up to maxWidth words wide,
        // some chunk must contain both endpoints, so the span is scorable in at least one chunk.
        var counts = Enumerable.Repeat(1, 30).ToArray();
        const int maxWidth = 4;
        var chunks = GlinerModel.PlanChunks(counts, 3, 12, maxWidth);

        Assert.True(chunks.Count > 1, "test should actually exercise multiple chunks");
        for (var a = 0; a < counts.Length; a++)
        for (var w = 0; w < maxWidth && a + w < counts.Length; w++)
        {
            var contained = chunks.Any(c => c.Start <= a && a + w < c.Start + c.Count);
            Assert.True(contained, $"span [{a},{a + w}] is not wholly inside any chunk");
        }
    }

    [Fact]
    public void Chunking_RecoversBoundaryStraddlingSpan_AtCorrectOffsets()
    {
        // 30 one-token words; small max_len forces overlapping chunks. This mirrors what Find does per chunk, but
        // drives the decode with a synthetic logit (no ONNX) firing one global span wherever a chunk contains it.
        var text = string.Join(" ", Enumerable.Repeat("w", 30));
        var words = GlinerModel.SplitWords(text);
        var counts = Enumerable.Repeat(1, words.Count).ToArray();
        var labels = new List<string> { "t" };
        const int prompt = 3, maxLen = 12, maxWidth = 4;

        var chunks = GlinerModel.PlanChunks(counts, prompt, maxLen, maxWidth);

        // Target words 7..9, a span straddling the first chunk's end (chunk 0 is [0,8): it holds word 7 but not
        // 8-9). Only an overlapping later chunk can contain it whole, so finding it proves the overlap recovers
        // boundary spans rather than dropping them.
        const int g0 = 7, g1 = 9;
        Assert.True(chunks[0].Start + chunks[0].Count <= g1, "target span must be one the first chunk cannot contain");

        var candidates = new List<GlinerModel.Entity>();
        foreach (var chunk in chunks)
        {
            var chunkWords = words.GetRange(chunk.Start, chunk.Count);

            float Logit(int i, int j, int l) =>
                chunkWords[i].Start == words[g0].Start && chunkWords[i + j].End == words[g1].End ? 10f : -10f;

            candidates.AddRange(GlinerModel.Decode(text, chunkWords, labels, 0.5, maxWidth, Logit));
        }

        var only = Assert.Single(GlinerModel.ResolveSpans(candidates));
        Assert.Equal(words[g0].Start, only.Start);
        Assert.Equal(words[g1].End, only.End);
        Assert.Equal(text.Substring(words[g0].Start, words[g1].End - words[g0].Start), only.Text);
    }

    [Fact]
    public void PlanChunks_SingleWordOverBudget_ThrowsRatherThanTruncate()
    {
        // budget = 8 - 1 - 1 = 6; the second word needs 7 sub-tokens and can never fit.
        var counts = new[] { 2, 7, 2 };

        var ex = Assert.Throws<InvalidOperationException>(
            () => GlinerModel.PlanChunks(counts, 1, 8, MaxWidth));
        Assert.Contains("single word", ex.Message);
    }

    [Fact]
    public void PlanChunks_PromptLeavesNoRoom_Throws()
    {
        // prompt 10 with max_len 10 leaves budget = 10 - 10 - 1 < 1.
        var ex = Assert.Throws<InvalidOperationException>(
            () => GlinerModel.PlanChunks(new[] { 1 }, 10, 10, MaxWidth));
        Assert.Contains("prompt", ex.Message);
    }

    [Fact]
    public void ResolveSpans_DropsDuplicateSpansFromOverlap_KeepingHighestScore()
    {
        // The same span rediscovered in two overlapping chunks, with slightly different scores.
        var candidates = new List<GlinerModel.Entity>
        {
            new("person", "Jane Roe", 0, 8, 0.80),
            new("person", "Jane Roe", 0, 8, 0.91)
        };

        var resolved = GlinerModel.ResolveSpans(candidates);

        var only = Assert.Single(resolved);
        Assert.Equal(0.91, only.Score);
    }

    [Fact]
    public void ResolveSpans_ResolvesCrossChunkOverlapByScoreAndOrdersByStart()
    {
        // Two overlapping spans from different chunks plus a separate one; greedy keeps the higher-scored overlap.
        var candidates = new List<GlinerModel.Entity>
        {
            new("person", "a", 0, 5, 0.90),
            new("person", "b", 3, 8, 0.70), // overlaps the first, lower score -> dropped
            new("person", "c", 10, 12, 0.60) // disjoint -> kept
        };

        var resolved = GlinerModel.ResolveSpans(candidates);

        Assert.Equal(new[] { 0, 10 }, resolved.Select(e => e.Start).ToArray());
    }
}
