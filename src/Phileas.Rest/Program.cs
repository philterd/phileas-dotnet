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

using MongoDB.Bson;
using MongoDB.Driver;
using Phileas.Filters.PhEye;
using Phileas.Rest;
using Phileas.Rest.Endpoints;
using Phileas.Rest.Ocr;
using Phileas.Rest.Storage;
using Phileas.Services;
using Phileas.Services.Disambiguation;
using Phileas.Services.Pdf;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var options = builder.Configuration.GetSection(PhileasRestOptions.SectionName).Get<PhileasRestOptions>()
              ?? new PhileasRestOptions();
builder.Services.AddSingleton(options);

// --- Storage: MongoDB (durable) + Valkey (cache) ---
builder.Services.AddSingleton<IMongoDatabase>(_ =>
    new MongoClient(options.MongoConnectionString).GetDatabase(options.MongoDatabase));

// Valkey speaks the Redis protocol. AbortOnConnectFail=false lets the service start even if the cache
// is briefly unavailable and reconnect transparently. When no connection string is configured, the
// context service runs cache-less (straight through to Mongo).
IConnectionMultiplexer? redis = null;
if (!string.IsNullOrWhiteSpace(options.ValkeyConnectionString))
{
    var config = ConfigurationOptions.Parse(options.ValkeyConnectionString);
    config.AbortOnConnectFail = false;
    redis = ConnectionMultiplexer.Connect(config);
    builder.Services.AddSingleton(redis);
}

builder.Services.AddSingleton(sp =>
    new MongoValkeyContextService(sp.GetRequiredService<IMongoDatabase>(), redis, options.ContextCacheTtlSeconds));
// The same instance is the IContextService baked into the filter pipeline.
builder.Services.AddSingleton<IContextService>(sp => sp.GetRequiredService<MongoValkeyContextService>());

builder.Services.AddSingleton(sp =>
    new PolicyRepository(sp.GetRequiredService<IMongoDatabase>(), options));

// --- Detection pipeline ---
// FilterService is stateless per call except for the shared context service; a single instance serves all
// requests. PdfFilterService takes the concrete FilterService, so redaction of every document type shares the
// same context store and thus the same referential-integrity replacements.
builder.Services.AddSingleton(sp =>
    new FilterService(false, new NoOpSpanDisambiguationService(), sp.GetRequiredService<IContextService>()));
builder.Services.AddSingleton<IFilterService>(sp => sp.GetRequiredService<FilterService>());

// PDF text extraction: the plain text layer, or (per config) OCR via Tesseract for scanned/image PDFs.
builder.Services.AddSingleton<ITextExtractor>(_ => options.Ocr.Mode switch
{
    OcrMode.Always => new TesseractTextExtractor(options.Ocr),
    OcrMode.Fallback => new TextLayerWithOcrFallbackExtractor(
        new PdfTextExtractor(), new TesseractTextExtractor(options.Ocr)),
    _ => new PdfTextExtractor()
});
builder.Services.AddSingleton(sp =>
    new Phileas.Services.Pdf.PdfFilterService(
        sp.GetRequiredService<FilterService>(), sp.GetRequiredService<ITextExtractor>()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Office/PDF documents are posted as the raw request body and can be large; lift Kestrel's body-size cap.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 256L * 1024 * 1024);

var app = builder.Build();

// Create indexes and (for local inference) load the GLiNER model once, at startup, so the first request is
// fast and a missing/broken model fails the container immediately instead of on first use.
app.Services.GetRequiredService<MongoValkeyContextService>().EnsureIndexes();
app.Services.GetRequiredService<PolicyRepository>().EnsureIndexes();
if (!string.IsNullOrEmpty(options.PhEyeModelPath))
{
    app.Logger.LogInformation("Loading GLiNER model from {ModelPath}", options.PhEyeModelPath);
    GlinerModel.GetShared(options.PhEyeModelPath);
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapFilterEndpoints();
app.MapPolicyEndpoints();
app.MapContextEndpoints();
// Philter-compatible surface (/api/filter, /api/health, /api/status) for philter-router / philter-sdk clients.
app.MapPhilterApiEndpoints();

// Liveness/readiness: verify Mongo and (if configured) Valkey are reachable.
app.MapGet("/health", (IMongoDatabase database) =>
{
    try
    {
        database.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
        redis?.GetDatabase().Ping();
        return Results.Ok(new { status = "healthy" });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unhealthy", error = ex.Message }, statusCode: 503);
    }
}).WithTags("Ops").WithName("Health");

app.Run();

// Exposes the implicit top-level Program class so the integration test project can host it via
// WebApplicationFactory<Program>.
public partial class Program;
