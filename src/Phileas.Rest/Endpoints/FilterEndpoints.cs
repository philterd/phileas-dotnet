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
using Phileas.Rest.Storage;
using Phileas.Services;
using Phileas.Services.Office;
using Phileas.Services.Pdf;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Rest.Endpoints;

/// <summary>
///     Native redaction endpoints: a single <c>POST /filter</c> whose request body is the raw document and whose
///     <c>Content-Type</c> header selects the handler (plain text, Word, Excel, or PDF). Policy and context are
///     the <c>p</c> and <c>c</c> query parameters. A companion <c>POST /explain</c> returns the detected spans
///     for plain text. (The Philter-compatible surface lives in <see cref="PhilterApiEndpoints" />.)
/// </summary>
public static class FilterEndpoints
{
    public static void MapFilterEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/filter", async (HttpContext http, string? p, string? c, string? d, bool? headerContext,
                IFilterService filterService, PdfFilterService pdfFilterService, PolicyRepository policies,
                PhileasRestOptions options) =>
            {
                var (policy, context, documentId, error) = Resolve(p, c, d, policies);
                if (error != null)
                    return error;

                http.Response.Headers[Redaction.DocumentIdHeader] = documentId;

                switch (Redaction.MediaType(http.Request))
                {
                    case Redaction.TextPlain:
                    {
                        var result = filterService.Filter(policy!, context, 0, await Redaction.ReadText(http.Request));
                        return Results.Text(result.FilteredText, Redaction.TextPlain);
                    }
                    case Redaction.Docx:
                    {
                        var (document, _) = WordDocumentRedactor.Redact(
                            await Redaction.ReadBytes(http.Request), Redaction.MakeFilter(filterService, policy!, context));
                        return Results.File(document, Redaction.Docx, "redacted.docx");
                    }
                    case Redaction.Xlsx:
                    {
                        // Give the detector each column's header as leading context. The per-request
                        // `headerContext` query parameter overrides the configured default.
                        var useHeaderContext = headerContext ?? options.Xlsx.UseHeaderContext;
                        var (document, _) = XlsxRedactor.Redact(
                            await Redaction.ReadBytes(http.Request), Redaction.MakeFilter(filterService, policy!, context),
                            useHeaderContext: useHeaderContext);
                        return Results.File(document, Redaction.Xlsx, "redacted.xlsx");
                    }
                    case Redaction.Pdf:
                    {
                        var result = pdfFilterService.Filter(
                            policy!, context, await Redaction.ReadBytes(http.Request), MimeType.ApplicationPdf);
                        return Results.File(result.Document, Redaction.Pdf, "redacted.pdf");
                    }
                    default:
                        return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
                }
            })
            .WithTags("Filter")
            .WithName("Filter")
            .WithSummary("Redact a document. The body is the raw document; Content-Type selects text/docx/xlsx/pdf. "
                         + "Policy and context are the 'p' and 'c' query parameters.");

        app.MapPost("/explain", async (HttpContext http, string? p, string? c, string? d,
                IFilterService filterService, PolicyRepository policies) =>
            {
                var (policy, context, documentId, error) = Resolve(p, c, d, policies);
                if (error != null)
                    return error;

                if (Redaction.MediaType(http.Request) != Redaction.TextPlain)
                    return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

                var result = filterService.Filter(policy!, context, 0, await Redaction.ReadText(http.Request));
                http.Response.Headers[Redaction.DocumentIdHeader] = documentId;

                var applied = result.Spans.Where(s => !s.Ignored).Select(SpanDto.From).ToList();
                var ignored = result.Spans.Where(s => s.Ignored).Select(SpanDto.From).ToList();
                return Results.Ok(new ExplainResponse(
                    result.FilteredText, context, documentId, new Explanation(applied, ignored)));
            })
            .WithTags("Filter")
            .WithName("Explain")
            .WithSummary("Redact plain text and return the applied/ignored spans. Content-Type must be text/plain.");
    }

    /// <summary>
    ///     Resolves the query parameters into a loaded policy, applying Philter's defaults (<c>p=default</c>,
    ///     <c>c=none</c>) and generating a document id when <c>d</c> is omitted. Returns a 404 result when the
    ///     named policy does not exist.
    /// </summary>
    private static (PhileasPolicy? Policy, string Context, string DocumentId, IResult? Error) Resolve(
        string? p, string? c, string? d, PolicyRepository policies)
    {
        var policyName = string.IsNullOrEmpty(p) ? "default" : p;
        var context = string.IsNullOrEmpty(c) ? "none" : c;
        var documentId = string.IsNullOrEmpty(d) ? Guid.NewGuid().ToString() : d;

        var policy = policies.Load(policyName);
        return policy == null
            ? (null, context, documentId, Results.NotFound($"Policy '{policyName}' not found."))
            : (policy, context, documentId, null);
    }
}
