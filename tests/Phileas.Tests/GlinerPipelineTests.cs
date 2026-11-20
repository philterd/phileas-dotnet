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
///     Model-free coverage of the deterministic GLiNER stages: word splitting, the <c>markerV0</c> input framing, and
///     the span decode. These run in CI without a model (no ONNX session, no SentencePiece file) by exercising the
///     <see cref="GlinerModel" /> static pipeline with a deterministic fake encoder and synthetic logits, so the parts
///     most likely to regress (prompt framing, <c>words_mask</c> alignment, span enumeration, threshold and overlap
///     resolution) are guarded even though the end-to-end ONNX run is only checked when a model is present.
/// </summary>
public class GlinerPipelineTests
{
    private const int EntId = 128002;
    private const int SepId = 128003;
    private const int ClsId = 1;
    private const int SepEndId = 2;
    private const int MaxWidth = 12;

    // Deterministic stand-in for the SentencePiece tokenizer: one id per character. Multi-character inputs therefore
    // produce multiple sub-tokens, which is what makes the words_mask "first sub-token only" behavior observable.
    private static IReadOnlyList<int> CharEncode(string s)
    {
        return s.Select(c => (int)c).ToList();
    }

    [Fact]
    public void SplitWords_KeepsHyphenatedRunsAndSplitsPunctuation()
    {
        var words = GlinerModel.SplitWords("Public 2024-03-02 j.public@example.com 123-45-6789");
        var texts = words.Select(w => w.Text).ToArray();

        // Dates and SSNs keep internal hyphens; "Q."-style punctuation and email separators split out.
        Assert.Equal(
            new[]
            {
                "Public", "2024-03-02",
                "j", ".", "public", "@", "example", ".", "com",
                "123-45-6789"
            },
            texts);
    }

    [Fact]
    public void SplitWords_TracksCharOffsets()
    {
        const string text = "John Q. Public";
        var words = GlinerModel.SplitWords(text);

        Assert.Equal(new[] { "John", "Q", ".", "Public" }, words.Select(w => w.Text).ToArray());
        // Each word's span must slice back to its own text.
        Assert.All(words, w => Assert.Equal(w.Text, text.Substring(w.Start, w.End - w.Start)));
        Assert.Equal(0, words[0].Start);
        Assert.Equal(4, words[0].End);
        Assert.Equal(8, words[3].Start);
        Assert.Equal(14, words[3].End);
    }

    [Fact]
    public void BuildInputs_FramesPromptWithSentinelsLabelsAndWords()
    {
        var words = GlinerModel.SplitWords("Al Bo"); // "Al" (chars 65,108), "Bo" (66,111)
        var labels = new List<string> { "x" }; // char 120

        var inputs = GlinerModel.BuildInputs(words, labels, CharEncode, EntId, SepId, MaxWidth);

        // [CLS] <<ENT>> x <<SEP>> A l B o [SEP]
        var expectedIds = new long[] { ClsId, EntId, 120, SepId, 65, 108, 66, 111, SepEndId };
        Assert.Equal(expectedIds, ReadRow(inputs.InputIds));

        // Attention is all-ones across the sequence; text_lengths is the word count.
        Assert.All(ReadRow(inputs.AttentionMask), v => Assert.Equal(1, v));
        Assert.Equal(2, inputs.TextLengths[0, 0]);
    }

    [Fact]
    public void BuildInputs_WordsMaskMarksFirstSubTokenOfEachWord()
    {
        var words = GlinerModel.SplitWords("Al Bo");
        var labels = new List<string> { "x" };

        var inputs = GlinerModel.BuildInputs(words, labels, CharEncode, EntId, SepId, MaxWidth);

        // Position:        CLS ENT  x  SEP  A  l  B  o  SEP
        // Word index (1-based) sits on each word's first sub-token only; everything else is 0.
        var expectedMask = new long[] { 0, 0, 0, 0, 1, 0, 2, 0, 0 };
        Assert.Equal(expectedMask, ReadRow(inputs.WordsMask));
    }

    [Fact]
    public void BuildInputs_SpanIdxAndMaskEnumerateCandidateSpans()
    {
        var words = GlinerModel.SplitWords("Al Bo"); // 2 words
        var labels = new List<string> { "x" };

        var inputs = GlinerModel.BuildInputs(words, labels, CharEncode, EntId, SepId, MaxWidth);

        // span k = i*maxWidth + j covers words [i .. i+j].
        Assert.Equal(0, inputs.SpanIdx[0, 0, 0]); // i=0
        Assert.Equal(0, inputs.SpanIdx[0, 0, 1]); // j=0 -> end word 0
        Assert.True(inputs.SpanMask[0, 0]); // [0..0] in range

        Assert.Equal(0, inputs.SpanIdx[0, 1, 0]);
        Assert.Equal(1, inputs.SpanIdx[0, 1, 1]); // [0..1] in range
        Assert.True(inputs.SpanMask[0, 1]);

        Assert.False(inputs.SpanMask[0, 2]); // [0..2] runs past the 2-word text

        Assert.Equal(1, inputs.SpanIdx[0, MaxWidth, 0]); // i=1, j=0
        Assert.True(inputs.SpanMask[0, MaxWidth]); // [1..1] in range
        Assert.False(inputs.SpanMask[0, MaxWidth + 1]); // [1..2] past the end
    }

    [Fact]
    public void Decode_ResolvesOverlapsByScoreAndOrdersByStart()
    {
        const string text = "Al Bo Cy"; // words: Al(0-2) Bo(3-5) Cy(6-8)
        var words = GlinerModel.SplitWords(text);
        var labels = new List<string> { "t" };

        // "Al Bo" (i0,j1) strongest; "Bo" (i1,j0) also fires but overlaps and loses; "Cy" (i2,j0) fires and is kept.
        var logits = Logits(new Dictionary<(int, int, int), float>
        {
            [(0, 1, 0)] = 5f, // sigmoid ~0.993 -> "Al Bo"
            [(1, 0, 0)] = 2f, // sigmoid ~0.881 -> "Bo", overlaps the winner
            [(2, 0, 0)] = 3f // sigmoid ~0.953 -> "Cy"
        });

        var entities = GlinerModel.Decode(text, words, labels, 0.5, MaxWidth, logits);

        Assert.Equal(2, entities.Count);
        Assert.Equal("Al Bo", entities[0].Text);
        Assert.Equal(0, entities[0].Start);
        Assert.Equal(5, entities[0].End);
        Assert.Equal("Cy", entities[1].Text);
        Assert.Equal(6, entities[1].Start);
        Assert.Equal(8, entities[1].End);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Decode_SpanAcrossLineBreak_IsNotReturned(string lineBreak)
    {
        // Two table rows: "Morgan Example" ends one, "SSN ( Jordan" starts the next.
        var text = "Morgan Example" + lineBreak + "SSN (Jordan";
        var words = GlinerModel.SplitWords(text); // Morgan Example SSN ( Jordan
        var labels = new List<string> { "name" };

        // The model scores the cross-row run highest; without the line boundary it would swallow both names.
        var logits = Logits(new Dictionary<(int, int, int), float>
        {
            [(0, 4, 0)] = 6f, // "Morgan Example ... Jordan", crosses the line break
            [(0, 1, 0)] = 4f, // "Morgan Example"
            [(4, 0, 0)] = 4f // "Jordan"
        });

        var entities = GlinerModel.Decode(text, words, labels, 0.5, MaxWidth, logits);

        Assert.Equal(new[] { "Morgan Example", "Jordan" }, entities.Select(e => e.Text));
    }

    [Fact]
    public void Decode_SpanWithinALine_AfterALineBreak_IsReturned()
    {
        const string text = "Taxpayer\nJordan Smith";
        var words = GlinerModel.SplitWords(text);
        var labels = new List<string> { "name" };

        var entities = GlinerModel.Decode(text, words, labels, 0.5, MaxWidth,
            Logits(new Dictionary<(int, int, int), float> { [(1, 1, 0)] = 5f }));

        var entity = Assert.Single(entities);
        Assert.Equal("Jordan Smith", entity.Text);
        Assert.Equal(9, entity.Start);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Decode_WrappedName_FoundOnlyAcrossTheLineBreak_IsReturned(string lineBreak)
    {
        // A name wrapped onto the next line: neither half fires alone, only the whole.
        var text = "approved by John" + lineBreak + "Smith today";
        var words = GlinerModel.SplitWords(text); // approved by John Smith today
        var labels = new List<string> { "name" };

        var entities = GlinerModel.Decode(text, words, labels, 0.5, MaxWidth,
            Logits(new Dictionary<(int, int, int), float> { [(2, 1, 0)] = 5f }));

        var entity = Assert.Single(entities);
        Assert.Equal("John" + lineBreak + "Smith", entity.Text);
    }

    [Fact]
    public void Decode_WeakSameLineSpan_StillBeatsAStrongCrossLineSpan()
    {
        const string text = "Morgan Example\nSSN (Jordan";
        var words = GlinerModel.SplitWords(text);
        var labels = new List<string> { "name" };

        var entities = GlinerModel.Decode(text, words, labels, 0.5, MaxWidth, Logits(new Dictionary<(int, int, int), float>
        {
            [(0, 4, 0)] = 8f, // cross-line, ~0.9997
            [(4, 0, 0)] = 0.5f // "Jordan", ~0.62
        }));

        // The cross-line span overlaps "Jordan", so only "Jordan" is kept.
        var entity = Assert.Single(entities);
        Assert.Equal("Jordan", entity.Text);
    }

    [Fact]
    public void Decode_CrossLineSpans_ResolveAmongThemselvesByScore()
    {
        const string text = "Al\nBo\nCy";
        var words = GlinerModel.SplitWords(text);
        var labels = new List<string> { "name" };

        var entities = GlinerModel.Decode(text, words, labels, 0.5, MaxWidth, Logits(new Dictionary<(int, int, int), float>
        {
            [(0, 1, 0)] = 3f, // "Al\nBo"
            [(1, 1, 0)] = 5f // "Bo\nCy" wins the overlap
        }));

        var entity = Assert.Single(entities);
        Assert.Equal("Bo\nCy", entity.Text);
    }

    [Fact]
    public void Decode_CrossLineSpan_ElsewhereOnThePage_IsKept()
    {
        // A row pair handled by same-line spans, then an unrelated wrapped name further down.
        const string text = "Jordan\nTaylor\nsent to Maria\nLopez";
        var words = GlinerModel.SplitWords(text); // Jordan Taylor sent to Maria Lopez
        var labels = new List<string> { "name" };

        var entities = GlinerModel.Decode(text, words, labels, 0.5, MaxWidth, Logits(new Dictionary<(int, int, int), float>
        {
            [(0, 0, 0)] = 4f, // "Jordan"
            [(1, 0, 0)] = 4f, // "Taylor"
            [(0, 1, 0)] = 6f, // "Jordan\nTaylor", overlaps both and loses
            [(4, 1, 0)] = 6f // "Maria\nLopez", overlaps nothing and is kept
        }));

        Assert.Equal(new[] { "Jordan", "Taylor", "Maria\nLopez" }, entities.Select(e => e.Text));
    }

    [Fact]
    public void ResolveSpans_DedupsRepeatedCrossLineSpans()
    {
        var a = new GlinerModel.Entity("name", "Al\nBo", 0, 5, 0.9);
        var b = new GlinerModel.Entity("name", "Al\nBo", 0, 5, 0.8);

        var entity = Assert.Single(GlinerModel.ResolveSpans(new List<GlinerModel.Entity> { b, a }));
        Assert.Equal(0.9, entity.Score);
    }

    [Fact]
    public void Decode_MapsLabelIndexToLabelString()
    {
        const string text = "Al";
        var words = GlinerModel.SplitWords(text);
        var labels = new List<string> { "person", "date" };

        // Only label index 1 ("date") clears the threshold for the single-word span.
        var logits = Logits(new Dictionary<(int, int, int), float> { [(0, 0, 1)] = 6f });

        var entities = GlinerModel.Decode(text, words, labels, 0.5, MaxWidth, logits);

        Assert.Single(entities);
        Assert.Equal("date", entities[0].Label);
        Assert.Equal("Al", entities[0].Text);
    }

    [Fact]
    public void Decode_BelowThreshold_ReturnsNothing()
    {
        const string text = "Al Bo";
        var words = GlinerModel.SplitWords(text);
        var labels = new List<string> { "t" };

        // Every span sits just under the threshold (sigmoid(0) = 0.5, which is not > 0.5).
        var entities = GlinerModel.Decode(text, words, labels, 0.5, MaxWidth, (_, _, _) => 0f);

        Assert.Empty(entities);
    }

    private static Func<int, int, int, float> Logits(Dictionary<(int, int, int), float> map, float fallback = -10f)
    {
        return (i, j, l) => map.TryGetValue((i, j, l), out var v) ? v : fallback;
    }

    private static long[] ReadRow(Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<long> tensor)
    {
        var len = tensor.Dimensions[1];
        var row = new long[len];
        for (var i = 0; i < len; i++)
            row[i] = tensor[0, i];
        return row;
    }
}
