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

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Phileas.Filters.PhEye;

/// <summary>
///     On-device GLiNER named-entity inference. Loads a self-contained GLiNER model directory (the ONNX graph,
///     the SentencePiece tokenizer, and <c>gliner_config.json</c>) and detects entities entirely in-process, with no
///     network call. GLiNER is zero-shot: the labels to detect are supplied at inference time as the prompt.
/// </summary>
/// <remarks>
///     The implementation reproduces the GLiNER <c>markerV0</c> span pipeline used by the reference Python package:
///     prompt construction (<c>[CLS] (&lt;&lt;ENT&gt;&gt; label)* &lt;&lt;SEP&gt;&gt; words [SEP]</c>), the six-input
///     ONNX signature (<c>input_ids</c>, <c>attention_mask</c>, <c>words_mask</c>, <c>text_lengths</c>,
///     <c>span_idx</c>, <c>span_mask</c>), and greedy non-overlapping flat-NER decoding. Outputs match the Python
///     reference span-for-span and score-for-score. The deterministic stages (<see cref="SplitWords" />,
///     <see cref="BuildInputs" />, <see cref="Decode" />) are factored out so they can be unit-tested without a model.
/// </remarks>
public sealed class GlinerModel : IDisposable
{
    // deberta-v3 reserves these ids for its sentinel tokens; GLiNER frames every prompt with them. The
    // SentencePiece model itself does not emit them, so they are added around the encoded pieces by hand.
    private const int ClsId = 1;
    private const int SepEndId = 2;

    // A word is a run of word-characters (keeping internal hyphens/underscores, so "123-45-6789" and "2024-03-02"
    // stay single words) or a single non-word, non-space character. Mirrors the model's whitespace+punctuation split.
    private static readonly Regex WordRegex = new(@"\w+(?:[-_]\w+)*|[^\w\s]", RegexOptions.Compiled);

    private readonly int _entId;
    private readonly int _maxLen;
    private readonly int _maxWidth;
    private readonly InferenceSession _session;
    private readonly int _sepId;
    private readonly SentencePieceTokenizer _tokenizer;

    /// <summary>
    ///     Loads the GLiNER model from <paramref name="modelPath" />. The directory must contain the SentencePiece
    ///     model (<c>spm.model</c>), <c>gliner_config.json</c>, and an exported ONNX graph (<c>model.onnx</c>,
    ///     <c>model_quantized.onnx</c>, or either under an <c>onnx/</c> subdirectory).
    /// </summary>
    /// <param name="modelPath">Filesystem path to the GLiNER model directory.</param>
    /// <exception cref="DirectoryNotFoundException">The model directory does not exist.</exception>
    /// <exception cref="FileNotFoundException">A required model file is missing.</exception>
    public GlinerModel(string modelPath)
    {
        if (!Directory.Exists(modelPath))
            throw new DirectoryNotFoundException($"GLiNER model directory not found: {modelPath}");

        var configPath = Path.Combine(modelPath, "gliner_config.json");
        if (!File.Exists(configPath))
            throw new FileNotFoundException($"gliner_config.json not found in model directory: {modelPath}");

        using (var configStream = File.OpenRead(configPath))
        {
            using var config = JsonDocument.Parse(configStream);
            var root = config.RootElement;
            _maxWidth = root.TryGetProperty("max_width", out var mw) ? mw.GetInt32() : 12;
            // max_len is the model's sub-token limit; sequences are chunked to never exceed it.
            _maxLen = root.TryGetProperty("max_len", out var ml) ? ml.GetInt32() : 384;
            // class_token_index is the id of <<ENT>>; <<SEP>> is the next reserved id.
            _entId = root.TryGetProperty("class_token_index", out var ct) ? ct.GetInt32() : 128002;
            _sepId = _entId + 1;
        }

        var spmPath = Path.Combine(modelPath, "spm.model");
        if (!File.Exists(spmPath))
            throw new FileNotFoundException($"spm.model not found in model directory: {modelPath}");

        using (var spmStream = File.OpenRead(spmPath))
        {
            // The GLiNER sentinel tokens are not in the SentencePiece vocabulary, so register them as specials;
            // the deberta [CLS]/[SEP]/[PAD]/[UNK] sentinels are added around the encoded pieces explicitly.
            _tokenizer = SentencePieceTokenizer.Create(spmStream, false, false,
                new Dictionary<string, int> { { "<<ENT>>", _entId }, { "<<SEP>>", _sepId } });
        }

        _session = new InferenceSession(ResolveOnnxPath(modelPath));
    }

    /// <summary>Releases the underlying ONNX inference session.</summary>
    public void Dispose()
    {
        _session.Dispose();
    }

    /// <summary>
    ///     Detects entities of the given <paramref name="labels" /> in <paramref name="text" />, returning every
    ///     non-overlapping span whose confidence meets <paramref name="threshold" />, ordered by start offset.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <param name="labels">The GLiNER detection prompt: the entity types to look for.</param>
    /// <param name="threshold">Minimum sigmoid confidence for a span to be returned.</param>
    /// <returns>The detected entities, ordered by character start offset.</returns>
    public IReadOnlyList<Entity> Find(string text, IReadOnlyList<string> labels, double threshold)
    {
        if (string.IsNullOrEmpty(text) || labels.Count == 0)
            return Array.Empty<Entity>();

        var words = SplitWords(text);
        if (words.Count == 0)
            return Array.Empty<Entity>();

        // Memoize sub-token encodings: the labels recur in every chunk, and words recur in chunk overlaps.
        var cache = new Dictionary<string, IReadOnlyList<int>>();

        IReadOnlyList<int> Encode(string s)
        {
            if (!cache.TryGetValue(s, out var ids))
            {
                ids = _tokenizer.EncodeToIds(s);
                cache[s] = ids;
            }

            return ids;
        }

        // Split the words into token-aware chunks so no sequence fed to the ONNX graph exceeds the model length.
        var promptTokens = PromptTokenCount(labels, Encode);
        var wordTokenCounts = new int[words.Count];
        for (var i = 0; i < words.Count; i++)
            wordTokenCounts[i] = Encode(words[i].Text).Count;
        var chunks = PlanChunks(wordTokenCounts, promptTokens, _maxLen, _maxWidth);

        // Each word already carries its absolute character span, so per-chunk decodes need no offset correction;
        // overlapping chunks may rediscover the same span, which the global ResolveSpans dedups.
        var candidates = new List<Entity>();
        foreach (var chunk in chunks)
        {
            var chunkWords = words.GetRange(chunk.Start, chunk.Count);
            var inputs = BuildInputs(chunkWords, labels, Encode, _entId, _sepId, _maxWidth);

            // Defensive: PlanChunks guarantees this, but never feed the graph a longer-than-supported sequence.
            var seqLen = inputs.InputIds.Dimensions[1];
            if (seqLen > _maxLen)
                throw new InvalidOperationException(
                    $"GLiNER chunk produced a sequence of {seqLen} sub-tokens, over max_len {_maxLen}.");

            using var results = _session.Run(inputs.ToFeeds());
            var logits = results.First().AsTensor<float>();
            candidates.AddRange(CollectSpans(text, chunkWords, labels, threshold, _maxWidth,
                (i, j, l) => logits[0, i, j, l]));
        }

        return ResolveSpans(candidates);
    }

    /// <summary>
    ///     Sub-token length of the fixed prompt frame <c>[CLS] (&lt;&lt;ENT&gt;&gt; label-pieces)* &lt;&lt;SEP&gt;&gt;</c>:
    ///     the <c>[CLS]</c>, each label's <c>&lt;&lt;ENT&gt;&gt;</c> marker plus its sub-tokens, and the trailing
    ///     <c>&lt;&lt;SEP&gt;&gt;</c>. The closing <c>[SEP]</c> is accounted for separately as the per-chunk budget's
    ///     reserved slot.
    /// </summary>
    private static int PromptTokenCount(IReadOnlyList<string> labels, Func<string, IReadOnlyList<int>> encode)
    {
        var count = 1; // [CLS]
        foreach (var label in labels)
            count += 1 + encode(label).Count; // <<ENT>> + label pieces
        count += 1; // <<SEP>> after the labels
        return count;
    }

    /// <summary>
    ///     Splits <paramref name="text" /> into words, tracking each word's character span. A word is a run of
    ///     word-characters (internal hyphens and underscores kept, so identifiers and dates stay whole) or a single
    ///     non-word, non-space character.
    /// </summary>
    internal static List<Word> SplitWords(string text)
    {
        var words = new List<Word>();
        foreach (Match m in WordRegex.Matches(text))
            words.Add(new Word(m.Value, m.Index, m.Index + m.Length));
        return words;
    }

    /// <summary>
    ///     Builds the GLiNER ONNX inputs for the given words and label prompt. The prompt is framed as
    ///     <c>[CLS] (&lt;&lt;ENT&gt;&gt; label-pieces)* &lt;&lt;SEP&gt;&gt; word-pieces* [SEP]</c>;
    ///     <c>words_mask</c> carries each word's 1-based index on its first sub-token (0 elsewhere) so per-token logits
    ///     align back to whole words; <c>span_idx</c>/<c>span_mask</c> enumerate every candidate span up to
    ///     <paramref name="maxWidth" /> words, masking spans that run past the text. The <paramref name="encode" />
    ///     delegate maps a string to its sub-token ids, letting tests substitute a deterministic encoder.
    /// </summary>
    internal static GlinerInputs BuildInputs(
        IReadOnlyList<Word> words,
        IReadOnlyList<string> labels,
        Func<string, IReadOnlyList<int>> encode,
        int entId,
        int sepId,
        int maxWidth)
    {
        var numWords = words.Count;

        var ids = new List<long> { ClsId };
        var wordsMask = new List<long> { 0 };

        foreach (var label in labels)
        {
            ids.Add(entId);
            wordsMask.Add(0);
            foreach (var t in encode(label))
            {
                ids.Add(t);
                wordsMask.Add(0);
            }
        }

        ids.Add(sepId);
        wordsMask.Add(0);

        for (var wi = 0; wi < numWords; wi++)
        {
            var sub = encode(words[wi].Text);
            for (var s = 0; s < sub.Count; s++)
            {
                ids.Add(sub[s]);
                wordsMask.Add(s == 0 ? wi + 1 : 0);
            }
        }

        ids.Add(SepEndId);
        wordsMask.Add(0);

        var seqLen = ids.Count;
        var numSpans = numWords * maxWidth;

        var spanIdx = new DenseTensor<long>(new[] { 1, numSpans, 2 });
        var spanMask = new DenseTensor<bool>(new[] { 1, numSpans });
        for (var i = 0; i < numWords; i++)
        for (var j = 0; j < maxWidth; j++)
        {
            var k = i * maxWidth + j;
            spanIdx[0, k, 0] = i;
            spanIdx[0, k, 1] = i + j;
            spanMask[0, k] = i + j < numWords;
        }

        var inputIds = new DenseTensor<long>(new[] { 1, seqLen });
        var attentionMask = new DenseTensor<long>(new[] { 1, seqLen });
        var wordsMaskTensor = new DenseTensor<long>(new[] { 1, seqLen });
        for (var i = 0; i < seqLen; i++)
        {
            inputIds[0, i] = ids[i];
            attentionMask[0, i] = 1;
            wordsMaskTensor[0, i] = wordsMask[i];
        }

        var textLengths = new DenseTensor<long>(new[] { 1, 1 });
        textLengths[0, 0] = numWords;

        return new GlinerInputs
        {
            InputIds = inputIds,
            AttentionMask = attentionMask,
            WordsMask = wordsMaskTensor,
            TextLengths = textLengths,
            SpanIdx = spanIdx,
            SpanMask = spanMask
        };
    }

    /// <summary>
    ///     Decodes raw span logits into entities. A span (word <c>i</c>, width <c>j</c>, label <c>l</c>) fires when
    ///     <c>sigmoid(logit) &gt; threshold</c>; firing spans are then resolved by greedy non-overlapping flat-NER
    ///     (highest score first) and returned ordered by start offset. <paramref name="logit" /> supplies the raw
    ///     logit for <c>(i, j, l)</c>, letting tests drive decoding with synthetic scores instead of an ONNX run.
    /// </summary>
    internal static List<Entity> Decode(
        string text,
        IReadOnlyList<Word> words,
        IReadOnlyList<string> labels,
        double threshold,
        int maxWidth,
        Func<int, int, int, float> logit)
    {
        return ResolveSpans(CollectSpans(text, words, labels, threshold, maxWidth, logit));
    }

    /// <summary>
    ///     Enumerates every candidate span that clears <paramref name="threshold" /> (word <c>i</c>, width <c>j</c>,
    ///     label <c>l</c> with <c>sigmoid(logit) &gt; threshold</c>) as an <see cref="Entity" /> carrying the absolute
    ///     character offsets from <paramref name="words" />. No overlap resolution is done here, so candidates from
    ///     several chunks can be pooled and resolved together by <see cref="ResolveSpans" />.
    /// </summary>
    private static List<Entity> CollectSpans(
        string text,
        IReadOnlyList<Word> words,
        IReadOnlyList<string> labels,
        double threshold,
        int maxWidth,
        Func<int, int, int, float> logit)
    {
        var numWords = words.Count;
        var numLabels = labels.Count;

        var candidates = new List<Entity>();
        for (var i = 0; i < numWords; i++)
        for (var j = 0; j < maxWidth; j++)
        {
            if (i + j >= numWords)
                continue;
            for (var l = 0; l < numLabels; l++)
            {
                var score = Sigmoid(logit(i, j, l));
                if (score <= threshold)
                    continue;
                var start = words[i].Start;
                var end = words[i + j].End;
                candidates.Add(new Entity(labels[l], text.Substring(start, end - start), start, end, score));
            }
        }

        return candidates;
    }

    /// <summary>
    ///     Resolves pooled candidate spans into the final entities by greedy non-overlapping flat-NER: take spans by
    ///     descending score, skip any that overlap one already chosen, return ordered by start offset. Because an
    ///     identical span overlaps its own duplicate, this also dedups the repeats that overlapping chunks produce,
    ///     keeping the highest-scored copy.
    /// </summary>
    internal static List<Entity> ResolveSpans(List<Entity> candidates)
    {
        var chosen = new List<Entity>();
        foreach (var c in candidates.OrderByDescending(x => x.Score))
            if (!chosen.Any(u => c.Start < u.End && u.Start < c.End))
                chosen.Add(c);

        return chosen.OrderBy(x => x.Start).ToList();
    }

    /// <summary>
    ///     Plans contiguous word ranges so each chunk's full sequence — the <paramref name="promptTokens" /> frame, the
    ///     chunk's word sub-tokens, and the trailing <c>[SEP]</c> — stays within <paramref name="maxLen" /> sub-tokens.
    ///     Consecutive chunks overlap by <c>maxWidth - 1</c> words so any span up to <paramref name="maxWidth" /> words
    ///     wide is wholly contained in at least one chunk (the overlap-induced duplicate spans are dropped later by
    ///     <see cref="ResolveSpans" />). Throws rather than truncate silently when the prompt leaves no room for text,
    ///     or when a single word's sub-tokens alone exceed the per-chunk budget.
    /// </summary>
    internal static List<WordRange> PlanChunks(
        IReadOnlyList<int> wordTokenCounts,
        int promptTokens,
        int maxLen,
        int maxWidth)
    {
        // Sub-tokens left for word pieces once the prompt and the trailing [SEP] are reserved.
        var budget = maxLen - promptTokens - 1;
        if (budget < 1)
            throw new InvalidOperationException(
                $"GLiNER label prompt needs {promptTokens} sub-tokens, leaving no room for text within max_len " +
                $"{maxLen}; use fewer or shorter labels.");

        var n = wordTokenCounts.Count;
        var chunks = new List<WordRange>();
        var i = 0;
        while (i < n)
        {
            var sum = 0;
            var j = i;
            while (j < n)
            {
                if (j == i && wordTokenCounts[j] > budget)
                    throw new InvalidOperationException(
                        $"A single word encodes to {wordTokenCounts[j]} sub-tokens, over the per-chunk budget of " +
                        $"{budget} (max_len {maxLen}); it cannot be processed without truncation.");
                if (j > i && sum + wordTokenCounts[j] > budget)
                    break;
                sum += wordTokenCounts[j];
                j++;
            }

            chunks.Add(new WordRange(i, j - i));
            if (j >= n)
                break;

            // Start the next chunk maxWidth-1 words back so a span straddling the cut stays whole in one chunk;
            // always advance by at least one word so progress is guaranteed even for tiny chunks.
            i = Math.Max(j - (maxWidth - 1), i + 1);
        }

        return chunks;
    }

    private static string ResolveOnnxPath(string modelPath)
    {
        string[] candidates =
        {
            Path.Combine(modelPath, "model.onnx"),
            Path.Combine(modelPath, "onnx", "model.onnx"),
            Path.Combine(modelPath, "model_quantized.onnx"),
            Path.Combine(modelPath, "onnx", "model_quantized.onnx")
        };
        foreach (var candidate in candidates)
            if (File.Exists(candidate))
                return candidate;
        throw new FileNotFoundException(
            $"No ONNX graph (model.onnx or model_quantized.onnx) found in model directory: {modelPath}");
    }

    private static double Sigmoid(float x)
    {
        return 1.0 / (1.0 + Math.Exp(-x));
    }

    /// <summary>A detected entity: its label, the matched text, the character span, and the model confidence.</summary>
    public readonly record struct Entity(string Label, string Text, int Start, int End, double Score);
}

/// <summary>A word and its character span within the source text.</summary>
internal readonly record struct Word(string Text, int Start, int End);

/// <summary>A contiguous range of words (<paramref name="Start" /> index, <paramref name="Count" />) forming one
///     token-aware inference chunk, as planned by <see cref="GlinerModel.PlanChunks" />.</summary>
internal readonly record struct WordRange(int Start, int Count);

/// <summary>The six ONNX input tensors of a single GLiNER inference, built by <see cref="GlinerModel.BuildInputs" />.</summary>
internal sealed class GlinerInputs
{
    public required DenseTensor<long> InputIds { get; init; }
    public required DenseTensor<long> AttentionMask { get; init; }
    public required DenseTensor<long> WordsMask { get; init; }
    public required DenseTensor<long> TextLengths { get; init; }
    public required DenseTensor<long> SpanIdx { get; init; }
    public required DenseTensor<bool> SpanMask { get; init; }

    /// <summary>Materializes the tensors as the named feed list the ONNX session expects.</summary>
    public List<NamedOnnxValue> ToFeeds()
    {
        return new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", InputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", AttentionMask),
            NamedOnnxValue.CreateFromTensor("words_mask", WordsMask),
            NamedOnnxValue.CreateFromTensor("text_lengths", TextLengths),
            NamedOnnxValue.CreateFromTensor("span_idx", SpanIdx),
            NamedOnnxValue.CreateFromTensor("span_mask", SpanMask)
        };
    }
}
