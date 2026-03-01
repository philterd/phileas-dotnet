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

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Policy.Filters;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters;

public class PhEyeFilter : AbstractFilter
{
    private readonly PhEyeConfiguration _configuration;
    private readonly bool _removePunctuation;
    private readonly Dictionary<string, double> _thresholds;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(_configuration.Timeout > 0 ? _configuration.Timeout : 30);
    }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = new List<Span>();

        var formattedInput = _removePunctuation
            ? System.Text.RegularExpressions.Regex.Replace(input, @"\p{P}", " ")
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
        httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrEmpty(_configuration.BearerToken))
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _configuration.BearerToken);

        HttpResponseMessage response;
        try
        {
            response = _httpClient.SendAsync(httpRequest).GetAwaiter().GetResult();
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
            if (_configuration.Labels.Count > 0 && !_configuration.Labels.Contains(phEyeSpan.Label, StringComparer.OrdinalIgnoreCase))
                continue;

            if (_thresholds.TryGetValue(phEyeSpan.Label.ToUpperInvariant(), out var threshold) && phEyeSpan.Score < threshold)
                continue;

            if (IsIgnored(phEyeSpan.Text))
                continue;

            var window = GetWindow(formattedInput, phEyeSpan.Start, phEyeSpan.End);
            var spanFilterType = phEyeSpan.Label.Equals("PERSON", StringComparison.OrdinalIgnoreCase)
                ? FilterType.Person
                : FilterType.Other;

            var replacement = GetReplacement(policy, context, phEyeSpan.Text, window, phEyeSpan.Score, phEyeSpan.Label, null);

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

    private sealed class PhEyeRequest
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("context")]
        public string Context { get; set; } = string.Empty;

        [JsonPropertyName("piece")]
        public int Piece { get; set; }

        [JsonPropertyName("labels")]
        public List<string> Labels { get; set; } = new List<string>();
    }

    private sealed class PhEyeSpan
    {
        [JsonPropertyName("start")]
        public int Start { get; set; }

        [JsonPropertyName("end")]
        public int End { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("score")]
        public double Score { get; set; }
    }
}
