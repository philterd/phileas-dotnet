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

using Microsoft.Net.Http.Headers;
using Phileas.Model;
using Phileas.Services;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Rest.Endpoints;

/// <summary>Shared helpers used by both the native (<c>/filter</c>) and Philter-compatible (<c>/api/filter</c>) endpoints.</summary>
internal static class Redaction
{
    public const string TextPlain = "text/plain";
    public const string Pdf = "application/pdf";
    public const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>The response header carrying the assigned document identifier (Philter convention).</summary>
    public const string DocumentIdHeader = "x-document-id";

    /// <summary>
    ///     Builds the detection delegate the Office redactors call once per text run. The context service is
    ///     already baked into <paramref name="filterService" />, so referential-integrity replacements are shared
    ///     with every other redaction in the same context.
    /// </summary>
    public static Func<string, TextFilterResult> MakeFilter(
        IFilterService filterService, PhileasPolicy policy, string context) =>
        text => filterService.Filter(policy, context, 0, text);

    /// <summary>Returns the request's media type without any charset/parameters, or <see langword="null" />.</summary>
    public static string? MediaType(HttpRequest request) =>
        string.IsNullOrEmpty(request.ContentType)
            ? null
            : MediaTypeHeaderValue.Parse(request.ContentType).MediaType.Value?.ToLowerInvariant();

    public static async Task<string> ReadText(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body);
        return await reader.ReadToEndAsync();
    }

    public static async Task<byte[]> ReadBytes(HttpRequest request)
    {
        using var memory = new MemoryStream();
        await request.Body.CopyToAsync(memory);
        return memory.ToArray();
    }
}
