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

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Phileas.Model;
using Phileas.Policy.Filters;

namespace Phileas.Filters.PhEye;

/// <summary>
///     Filter that detects entities using either a remote PhEye NLP service via HTTP
///     or a local ONNX BERT-based NER model. The mode is determined by the presence
///     of model path configuration.
/// </summary>
public class PhEyeFilter : AbstractFilter, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly PhEyeConfiguration _configuration;
    private readonly HttpClient? _httpClient;
    private readonly bool _removePunctuation;
    private readonly Dictionary<string, double> _thresholds;
    
    private readonly InferenceSession? _session;
    private readonly BertTokenizer? _tokenizer;
    private readonly Dictionary<int, string>? _idToLabel;
    private readonly bool _useLocalModel;

    /// <summary>
    ///     Initializes a new <see cref="PhEyeFilter" />.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration (strategies, ignored terms, etc.).</param>
    /// <param name="phEyeConfiguration">Connection and label settings for the PhEye service or local model paths.</param>
    /// <param name="removePunctuation">
    ///     When <see langword="true" />, punctuation is stripped from the input before processing.
    /// </param>
    /// <param name="thresholds">Per-label minimum confidence thresholds; entities below the threshold are discarded.</param>
    /// <param name="httpClient">
    ///     Optional pre-configured <see cref="HttpClient" />; a new instance is created when
    ///     <see langword="null" /> and using remote service.
    /// </param>
    public PhEyeFilter(
        FilterConfiguration configuration,
        PhEyeConfiguration phEyeConfiguration,
        bool removePunctuation,
        Dictionary<string, double> thresholds,
        HttpClient? httpClient = null)
        : base(FilterType.PhEye, configuration)
    {
        _configuration = phEyeConfiguration;
        _removePunctuation = removePunctuation;
        _thresholds = thresholds;

        _useLocalModel = !string.IsNullOrEmpty(_configuration.ModelPath) && 
                         !string.IsNullOrEmpty(_configuration.VocabPath);

        if (_useLocalModel)
        {
            _session = new InferenceSession(_configuration.ModelPath!);
            _tokenizer = new BertTokenizer(_configuration.VocabPath!);
            _idToLabel = InitializeLabelMapping();
        }
        else
        {
            _httpClient = httpClient ?? new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.Timeout > 0 ? _configuration.Timeout : 30);
        }
    }

    /// <summary>
    ///     Scans <paramref name="input" /> for named entities using either the PhEye service or local ONNX model.
    /// </summary>
    /// <param name="policy">The active policy providing per-filter settings.</param>
    /// <param name="context">The context identifier used for referential-integrity replacements.</param>
    /// <param name="piece">Zero-based piece index within a multi-part document.</param>
    /// <param name="input">The plain-text string to search.</param>
    /// <returns>A <see cref="Filtered" /> containing all detected named entities.</returns>
    public override Filtered Filter(Policy.Policy policy, string context, int piece, string input)
    {
        return _useLocalModel
            ? FilterWithLocalModel(policy, context, piece, input)
            : FilterWithRemoteService(policy, context, piece, input);
    }

    private Filtered FilterWithLocalModel(Policy.Policy policy, string context, int piece, string input)
    {
        var spans = new List<Span>();

        if (string.IsNullOrWhiteSpace(input))
            return new Filtered(context, piece, spans);

        var tokens = _tokenizer!.Tokenize(input);
        var inputIds = _tokenizer.ConvertTokensToIds(tokens);
        
        var inputIdsArray = inputIds.ToArray();
        var attentionMask = Enumerable.Repeat(1L, inputIds.Count).ToArray();

        var inputIdsTensor = new DenseTensor<long>(new[] { 1, inputIds.Count });
        for (var i = 0; i < inputIdsArray.Length; i++)
            inputIdsTensor[0, i] = inputIdsArray[i];

        var attentionMaskTensor = new DenseTensor<long>(new[] { 1, attentionMask.Length });
        for (var i = 0; i < attentionMask.Length; i++)
            attentionMaskTensor[0, i] = attentionMask[i];

        var tokenTypeIdsTensor = new DenseTensor<long>(new[] { 1, inputIds.Count });
        for (var i = 0; i < inputIds.Count; i++)
            tokenTypeIdsTensor[0, i] = 0L;

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
        };

        using var results = _session!.Run(inputs);
        var logitsOutput = results.FirstOrDefault(r => r.Name == "logits");
        if (logitsOutput == null)
        {
            return new Filtered(context, piece, spans);
        }
        
        var output = logitsOutput.AsEnumerable<float>().ToArray();
        var outputShape = logitsOutput.AsTensor<float>().Dimensions.ToArray();

        var numTokens = outputShape[1];
        var numLabels = outputShape[2];

        var predictions = new List<(string token, int labelId, float confidence, int tokenIndex)>();

        for (var i = 0; i < numTokens; i++)
        {
            var maxIndex = 0;
            var maxValue = output[i * numLabels];

            for (var j = 1; j < numLabels; j++)
            {
                var value = output[i * numLabels + j];
                if (value > maxValue)
                {
                    maxValue = value;
                    maxIndex = j;
                }
            }

            if (maxIndex > 0 && i < tokens.Count)
            {
                var confidence = Softmax(output, i * numLabels, numLabels)[maxIndex];
                predictions.Add((tokens[i], maxIndex, confidence, i));
            }
        }

        var entities = GroupEntities(predictions, tokens, input);

        foreach (var entity in entities)
        {
            if (IsIgnored(entity.text))
                continue;

            if (_thresholds.TryGetValue(entity.label.ToUpperInvariant(), out var threshold) &&
                entity.confidence < threshold)
                continue;

            var filterType = MapLabelToFilterType(entity.label);
            var window = GetWindow(input, entity.start, entity.end);
            var replacement = GetReplacement(policy, context, entity.text, window, entity.confidence,
                entity.label, null);

            var span = Span.Make(
                entity.start, entity.end,
                filterType, context, entity.confidence, entity.text,
                replacement.Value, replacement.Salt,
                false, replacement.Applied,
                window, Priority);
            span.Classification = entity.label;

            spans.Add(span);
        }

        var filteredSpans = Span.DropOverlappingSpans(spans).ToList();
        return new Filtered(context, piece, filteredSpans);
    }

    private Filtered FilterWithRemoteService(Policy.Policy policy, string context, int piece, string input)
    {
        var spans = new List<Span>();

        var formattedInput = _removePunctuation
            ? Regex.Replace(input, @"\p{P}", " ")
            : input;

        var request = new PhEyeRequest
        {
            Text = input,
            Context = context,
            Piece = piece,
            Labels = _configuration.Labels
        };

        var url = _configuration.Endpoint.TrimEnd('/') + "/find";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrEmpty(_configuration.BearerToken))
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.BearerToken);

        HttpResponseMessage response;
        try
        {
            response = _httpClient!.SendAsync(httpRequest).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            throw new IOException($"Unable to connect to pheye service at {url}.", ex);
        }

        if (!response.IsSuccessStatusCode)
            throw new IOException($"pheye service returned status {(int)response.StatusCode}.");

        var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (string.IsNullOrEmpty(responseBody))
            return new Filtered(context, piece, spans);

        var phEyeSpans = JsonSerializer.Deserialize<List<PhEyeSpan>>(responseBody, JsonOptions);
        if (phEyeSpans == null || phEyeSpans.Count == 0)
            return new Filtered(context, piece, spans);

        foreach (var phEyeSpan in phEyeSpans)
        {
            if (_configuration.Labels.Count > 0 &&
                !_configuration.Labels.Contains(phEyeSpan.Label, StringComparer.OrdinalIgnoreCase))
                continue;

            if (_thresholds.TryGetValue(phEyeSpan.Label.ToUpperInvariant(), out var threshold) &&
                phEyeSpan.Score < threshold)
                continue;

            if (IsIgnored(phEyeSpan.Text))
                continue;

            var window = GetWindow(formattedInput, phEyeSpan.Start, phEyeSpan.End);
            var spanFilterType = phEyeSpan.Label.Equals("PERSON", StringComparison.OrdinalIgnoreCase)
                ? FilterType.Person
                : FilterType.Other;

            var replacement = GetReplacement(policy, context, phEyeSpan.Text, window, phEyeSpan.Score, phEyeSpan.Label,
                null);

            if (string.Equals(replacement.Value, phEyeSpan.Text, StringComparison.OrdinalIgnoreCase))
                continue;

            var span = Span.Make(
                phEyeSpan.Start, phEyeSpan.End,
                spanFilterType, context, phEyeSpan.Score, phEyeSpan.Text,
                replacement.Value, replacement.Salt,
                false, replacement.Applied,
                window, Priority);
            span.Classification = phEyeSpan.Label;

            spans.Add(span);
        }

        return new Filtered(context, piece, Span.DropOverlappingSpans(spans));
    }

    private Dictionary<int, string> InitializeLabelMapping()
    {
        return new Dictionary<int, string>
        {
            { 0, "O" },
            { 1, "B-MISC" },
            { 2, "I-MISC" },
            { 3, "B-PER" },
            { 4, "I-PER" },
            { 5, "B-ORG" },
            { 6, "I-ORG" },
            { 7, "B-LOC" },
            { 8, "I-LOC" }
        };
    }

    private FilterType MapLabelToFilterType(string label)
    {
        return label switch
        {
            "PER" => FilterType.Person,
            "ORG" => FilterType.Other,
            "LOC" => FilterType.LocationCity,
            "MISC" => FilterType.Other,
            _ => FilterType.Other
        };
    }

    private float[] Softmax(float[] logits, int offset, int length)
    {
        var result = new float[length];
        var max = logits.Skip(offset).Take(length).Max();
        var sum = 0f;

        for (var i = 0; i < length; i++)
        {
            result[i] = MathF.Exp(logits[offset + i] - max);
            sum += result[i];
        }

        for (var i = 0; i < length; i++)
            result[i] /= sum;

        return result;
    }

    private List<(string text, string label, int start, int end, double confidence)> GroupEntities(
        List<(string token, int labelId, float confidence, int tokenIndex)> predictions,
        List<string> tokens,
        string input)
    {
        var entities = new List<(string text, string label, int start, int end, double confidence)>();
        var currentEntity = new List<(string token, string label, float confidence, int tokenIndex)>();
        string? currentLabel = null;

        foreach (var pred in predictions)
        {
            var label = _idToLabel![pred.labelId];

            if (label.StartsWith("B-"))
            {
                if (currentEntity.Count > 0)
                {
                    var entity = FinishEntity(currentEntity, tokens, input, currentLabel!);
                    if (entity.HasValue)
                        entities.Add(entity.Value);
                    currentEntity.Clear();
                }

                currentLabel = label[2..];
                currentEntity.Add((pred.token, label, pred.confidence, pred.tokenIndex));
            }
            else if (label.StartsWith("I-") && currentLabel == label[2..])
            {
                currentEntity.Add((pred.token, label, pred.confidence, pred.tokenIndex));
            }
            else if (label != "O" && currentEntity.Count > 0)
            {
                var entity = FinishEntity(currentEntity, tokens, input, currentLabel!);
                if (entity.HasValue)
                    entities.Add(entity.Value);
                currentEntity.Clear();
                currentLabel = null;
            }
        }

        if (currentEntity.Count > 0)
        {
            var entity = FinishEntity(currentEntity, tokens, input, currentLabel!);
            if (entity.HasValue)
                entities.Add(entity.Value);
        }

        return entities;
    }

    private (string text, string label, int start, int end, double confidence)? FinishEntity(
        List<(string token, string label, float confidence, int tokenIndex)> entityTokens,
        List<string> allTokens,
        string input,
        string label)
    {
        if (entityTokens.Count == 0)
            return null;

        var reconstructed = ReconstructText(entityTokens.Select(e => e.token).ToList());
        var avgConfidence = entityTokens.Average(e => e.confidence);

        var startPos = FindTextPosition(input, reconstructed, 0);
        if (startPos < 0)
            return null;

        var endPos = startPos + reconstructed.Length;

        return (reconstructed, label, startPos, endPos, avgConfidence);
    }

    private string ReconstructText(List<string> tokens)
    {
        var sb = new StringBuilder();

        foreach (var token in tokens)
        {
            if (token.StartsWith("##"))
                sb.Append(token[2..]);
            else
            {
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append(token);
            }
        }

        return sb.ToString();
    }

    private int FindTextPosition(string input, string text, int startIndex)
    {
        return input.IndexOf(text, startIndex, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Releases all resources used by the <see cref="PhEyeFilter" />, including the ONNX inference session if used.
    /// </summary>
    public void Dispose()
    {
        _session?.Dispose();
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class PhEyeRequest
    {
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;

        [JsonPropertyName("context")] public string Context { get; set; } = string.Empty;

        [JsonPropertyName("piece")] public int Piece { get; set; }

        [JsonPropertyName("labels")] public List<string> Labels { get; set; } = new();
    }

    private sealed class PhEyeSpan
    {
        [JsonPropertyName("start")] public int Start { get; set; }

        [JsonPropertyName("end")] public int End { get; set; }

        [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;

        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;

        [JsonPropertyName("score")] public double Score { get; set; }
    }
}

/// <summary>
///     Simple BERT tokenizer for processing text input.
/// </summary>
internal class BertTokenizer
{
    private readonly Dictionary<string, int> _vocab;
    private readonly Dictionary<int, string> _idToToken;
    private const int MaxSequenceLength = 512;

    public BertTokenizer(string vocabPath)
    {
        _vocab = new Dictionary<string, int>();
        _idToToken = new Dictionary<int, string>();
        LoadVocabulary(vocabPath);
    }

    private void LoadVocabulary(string vocabPath)
    {
        var lines = File.ReadAllLines(vocabPath);
        for (var i = 0; i < lines.Length; i++)
        {
            var token = lines[i].Trim();
            _vocab[token] = i;
            _idToToken[i] = token;
        }
    }

    public List<string> Tokenize(string text)
    {
        var tokens = new List<string> { "[CLS]" };

        var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            var wordTokens = WordPieceTokenize(word);
            tokens.AddRange(wordTokens);

            if (tokens.Count >= MaxSequenceLength - 1)
                break;
        }

        tokens.Add("[SEP]");

        return tokens;
    }

    private List<string> WordPieceTokenize(string word)
    {
        var tokens = new List<string>();

        if (_vocab.ContainsKey(word))
        {
            tokens.Add(word);
            return tokens;
        }

        var start = 0;
        while (start < word.Length)
        {
            var end = word.Length;
            string? subToken = null;

            while (start < end)
            {
                var substr = word.Substring(start, end - start);
                if (start > 0)
                    substr = "##" + substr;

                if (_vocab.ContainsKey(substr))
                {
                    subToken = substr;
                    break;
                }

                end--;
            }

            if (subToken == null)
            {
                tokens.Add("[UNK]");
                break;
            }

            tokens.Add(subToken);
            start = end;
        }

        return tokens;
    }

    public List<int> ConvertTokensToIds(List<string> tokens)
    {
        return tokens.Select(token => _vocab.ContainsKey(token) ? _vocab[token] : _vocab["[UNK]"]).ToList();
    }
}

