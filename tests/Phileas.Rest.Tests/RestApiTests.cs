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

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Phileas.Rest.Tests;

/// <summary>
///     End-to-end HTTP tests against the Phileas.Rest service running over real MongoDB + Valkey containers.
///     Each test skips (rather than fails) when Docker is unavailable.
/// </summary>
[Collection(RestApiCollection.Name)]
public sealed class RestApiTests
{
    // A minimal policy that redacts email addresses — no PhEye/GLiNER model required.
    private const string EmailPolicyJson = "{\"identifiers\":{\"emailAddress\":{}}}";
    private const string Email = "test@example.com";

    private readonly RestApiFixture _fixture;

    public RestApiTests(RestApiFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task Health_ReportsHealthy()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");

        var response = await _fixture.Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("healthy", doc.RootElement.GetProperty("status").GetString());
    }

    [SkippableFact]
    public async Task Policy_Crud_RoundTrips()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        var client = _fixture.Client;

        var put = await UpsertPolicy("crud", EmailPolicyJson);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var list = await client.GetFromJsonAsync<List<string>>("/policies");
        Assert.Contains("crud", list!);

        var get = await client.GetAsync("/policies/crud");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var delete = await client.DeleteAsync("/policies/crud");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var afterDelete = await client.GetAsync("/policies/crud");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [SkippableFact]
    public async Task Filter_Text_RedactsEmail()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        var client = _fixture.Client;

        await UpsertPolicy("text", EmailPolicyJson);

        var response = await client.PostAsync("/filter?p=text&c=ctx", TextBody($"Contact {Email} for details."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("x-document-id"));
        Assert.DoesNotContain(Email, await response.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task Explain_Text_ReturnsAppliedSpans()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        var client = _fixture.Client;

        await UpsertPolicy("explain", EmailPolicyJson);

        var response = await client.PostAsync("/explain?p=explain&c=ctx", TextBody($"Contact {Email}."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(Email, doc.RootElement.GetProperty("filteredText").GetString());
        Assert.NotEmpty(doc.RootElement.GetProperty("explanation").GetProperty("appliedSpans").EnumerateArray());
    }

    [SkippableFact]
    public async Task Filter_UnknownPolicy_ReturnsNotFound()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");

        var response = await _fixture.Client.PostAsync("/filter?p=does-not-exist&c=ctx", TextBody("anything"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Filter_UnsupportedContentType_Returns415()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        var client = _fixture.Client;

        await UpsertPolicy("unsupported", EmailPolicyJson);
        var body = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/filter?p=unsupported&c=ctx", body);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [SkippableFact]
    public async Task Filter_Docx_RedactsEmail()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        var client = _fixture.Client;

        await UpsertPolicy("docx", EmailPolicyJson);

        var docx = BuildDocx($"Please email {Email} today.");
        var body = new ByteArrayContent(docx);
        body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        var response = await client.PostAsync("/filter?p=docx&c=ctx", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var redacted = await response.Content.ReadAsByteArrayAsync();
        Assert.DoesNotContain(Email, ExtractDocxText(redacted));
    }

    [SkippableFact]
    public async Task Contexts_CreateListDelete()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        var client = _fixture.Client;

        var create = await client.PostAsync("/contexts/mycontext", content: null);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var list = await client.GetFromJsonAsync<List<string>>("/contexts");
        Assert.Contains("mycontext", list!);

        var delete = await client.DeleteAsync("/contexts/mycontext");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    // ---- Philter-compatible surface (used by philter-router / philter-sdk-java) ----

    [SkippableFact]
    public async Task PhilterApi_Health_ReturnsStatusResponse()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");

        var response = await _fixture.Client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.True(doc.RootElement.TryGetProperty("applicationVersion", out _));
        Assert.True(doc.RootElement.TryGetProperty("redactionPolicySchemaVersion", out _));
    }

    [SkippableFact]
    public async Task PhilterApi_FilterText_RedactsEmail()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        var client = _fixture.Client;

        await UpsertPolicy("papi-text", EmailPolicyJson);

        // No filename => text request, matching the SDK's text filter call.
        var response = await client.PostAsync("/api/filter?p=papi-text&c=ctx", TextBody($"Contact {Email}."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("x-document-id"));
        Assert.DoesNotContain(Email, await response.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task PhilterApi_FilterDocx_DispatchesByFilename_NotContentType()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        var client = _fixture.Client;

        await UpsertPolicy("papi-docx", EmailPolicyJson);

        // The philter-sdk-java client always sends files as application/pdf and conveys the true type via
        // ?filename=. Here the body is a .docx sent as application/pdf; dispatch must key off the filename.
        var body = new ByteArrayContent(BuildDocx($"Please email {Email} today."));
        body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

        var response = await client.PostAsync("/api/filter?p=papi-docx&c=ctx&filename=report.docx", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var redacted = await response.Content.ReadAsByteArrayAsync();
        Assert.DoesNotContain(Email, ExtractDocxText(redacted));
    }

    private Task<HttpResponseMessage> UpsertPolicy(string name, string policyJson) =>
        _fixture.Client.PutAsJsonAsync($"/policies/{name}", new { json = policyJson });

    private static StringContent TextBody(string text) => new(text, Encoding.UTF8, "text/plain");

    private static byte[] BuildDocx(string text)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text(text)))));
            main.Document.Save();
        }

        return stream.ToArray();
    }

    private static string ExtractDocxText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, isEditable: false);
        var builder = new StringBuilder();
        foreach (var text in document.MainDocumentPart!.Document.Descendants<Text>())
            builder.Append(text.Text);
        return builder.ToString();
    }
}
