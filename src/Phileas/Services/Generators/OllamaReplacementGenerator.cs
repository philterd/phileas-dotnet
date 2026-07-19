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
using System.Text.Json;
using Phileas.Policy;

namespace Phileas.Services.Generators;

/// <summary>
///     <see cref="IReplacementGenerator" /> that calls a local Ollama-compatible <c>/api/generate</c> endpoint to
///     produce a replacement value. The endpoint is expected to resolve inside the deployment boundary so detected
///     values are not sent to a third party.
/// </summary>
public class OllamaReplacementGenerator : IReplacementGenerator
{
    private readonly Generator _generator;
    private readonly HttpClient _httpClient;

    /// <summary>
    ///     Initializes a new <see cref="OllamaReplacementGenerator" />.
    /// </summary>
    /// <param name="generator">The generator definition (endpoint, model, prompt, timeout).</param>
    /// <param name="httpClient">Optional pre-configured <see cref="HttpClient" />; a new instance is created when <see langword="null" />.</param>
    public OllamaReplacementGenerator(Generator generator, HttpClient? httpClient = null)
    {
        _generator = generator;
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <inheritdoc />
    public string Generate(string token, string? label)
    {
        var prompt = (_generator.Prompt ?? string.Empty)
            .Replace("{{token}}", token)
            .Replace("{{label}}", label ?? string.Empty);

        // Request a single, complete response rather than a stream of chunks so the body is one JSON object.
        var requestBody = JsonSerializer.Serialize(new
        {
            model = _generator.Model,
            prompt,
            stream = false
        });

        var endpoint = (_generator.Endpoint ?? string.Empty).TrimEnd('/') + "/api/generate";

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        // timeoutMs is required by the schema, so a generator can never block the pipeline indefinitely.
        var timeoutMs = _generator.TimeoutMs ?? 0;
        using var cts = timeoutMs > 0 ? new CancellationTokenSource(timeoutMs) : new CancellationTokenSource();

        using var response = _httpClient.SendAsync(request, cts.Token).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Generator endpoint returned status {(int)response.StatusCode}.");

        var responseBody = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(responseBody))
            throw new InvalidOperationException("Generator endpoint returned an empty response.");

        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("response", out var responseElement))
            throw new InvalidOperationException("Generator response did not contain a 'response' field.");

        var replacement = responseElement.GetString();
        if (string.IsNullOrWhiteSpace(replacement))
            throw new InvalidOperationException("Generator produced a blank replacement.");

        // Models often wrap the value in surrounding whitespace or newlines; return only the value.
        return replacement.Trim();
    }
}
