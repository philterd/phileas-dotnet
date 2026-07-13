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

using System.Reflection;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using Phileas.Model;
using Phileas.Rest.Storage;
using Phileas.Services;
using Phileas.Services.Office;
using Phileas.Services.Pdf;
using StackExchange.Redis;

namespace Phileas.Rest.Endpoints;

/// <summary>
///     Philter-compatible API surface, so a Philter client — notably
///     <a href="https://github.com/philterd/philter-router">philter-router</a> via philter-sdk-java — can use
///     this service as a redaction engine unchanged.
///     <para>
///         The SDK posts to <c>POST /api/filter?c=&amp;p=&amp;filename=&amp;async=</c> with the raw document as
///         the body. It always sets <c>Content-Type: application/pdf</c> for files and conveys the true type via
///         <c>filename</c>, so the document type here is taken from the <b>filename extension</b> (falling back
///         to plain text when <c>filename</c> is absent). The redacted document is returned in the body with the
///         assigned id in the <c>x-document-id</c> header. <c>GET /api/health</c> and <c>GET /api/status</c>
///         return a Philter <c>StatusResponse</c>.
///     </para>
/// </summary>
public static class PhilterApiEndpoints
{
    private static readonly string ApplicationVersion =
        typeof(PhilterApiEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(PhilterApiEndpoints).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    public static void MapPhilterApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/filter", async (HttpContext http, string? c, string? p, string? filename, bool? async,
                IFilterService filterService, PdfFilterService pdfFilterService, PolicyRepository policies,
                PhileasRestOptions options) =>
            {
                var policyName = string.IsNullOrEmpty(p) ? "default" : p;
                var context = string.IsNullOrEmpty(c) ? "none" : c;

                var policy = policies.Load(policyName);
                if (policy == null)
                    return Results.NotFound($"Policy '{policyName}' not found.");

                // Philter assigns a document id and returns it in the x-document-id header. (async is accepted
                // for wire-compatibility; this service always filters synchronously.)
                http.Response.Headers[Redaction.DocumentIdHeader] = Guid.NewGuid().ToString();
                var filter = Redaction.MakeFilter(filterService, policy, context);

                // No filename => a text request (the SDK passes filename=null for text).
                if (string.IsNullOrWhiteSpace(filename))
                {
                    var textResult = filterService.Filter(policy, context, 0, await Redaction.ReadText(http.Request));
                    return Results.Text(textResult.FilteredText, Redaction.TextPlain);
                }

                // The SDK sends every file as application/pdf, so the true type comes from the filename extension.
                var bytes = await Redaction.ReadBytes(http.Request);
                switch (Path.GetExtension(filename).ToLowerInvariant())
                {
                    case ".docx":
                        return Results.File(
                            WordDocumentRedactor.Redact(bytes, filter).Document, Redaction.Docx, filename);
                    case ".xlsx":
                        return Results.File(
                            XlsxRedactor.Redact(bytes, filter, useHeaderContext: options.Xlsx.UseHeaderContext).Document,
                            Redaction.Xlsx, filename);
                    case ".pdf":
                        return Results.File(
                            pdfFilterService.Filter(policy, context, bytes, MimeType.ApplicationPdf).Document,
                            Redaction.Pdf, filename);
                    case ".txt":
                    case "":
                        var fileText = filterService.Filter(policy, context, 0, Encoding.UTF8.GetString(bytes));
                        return Results.Text(fileText.FilteredText, Redaction.TextPlain);
                    default:
                        return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
                }
            })
            .WithTags("Philter API")
            .WithName("PhilterFilter")
            .WithSummary("Philter-compatible filter. Body is the raw document; type is taken from the 'filename' "
                         + "query parameter's extension (plain text when absent). Params: c, p, filename, async.");

        // Philter health/status. Both return a StatusResponse; philter clients use them for liveness checks.
        app.MapGet("/api/health", Health).WithTags("Philter API").WithName("PhilterHealth");
        app.MapGet("/api/status", Health).WithTags("Philter API").WithName("PhilterStatus");
    }

    private static IResult Health(IMongoDatabase database, IServiceProvider services)
    {
        try
        {
            database.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
            services.GetService<IConnectionMultiplexer>()?.GetDatabase().Ping();
            return Results.Ok(Status("Healthy"));
        }
        catch
        {
            return Results.Json(Status("Unhealthy"), statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    // Matches philter-sdk-java's StatusResponse (applicationVersion, gitCommit, redactionPolicySchemaVersion, status).
    private static object Status(string status) => new
    {
        applicationVersion = ApplicationVersion,
        gitCommit = string.Empty,
        redactionPolicySchemaVersion = "1.1.0",
        status
    };
}
