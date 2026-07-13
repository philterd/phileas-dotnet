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

using Phileas.Rest.Storage;

namespace Phileas.Rest.Endpoints;

/// <summary>
///     Management endpoints for contexts and their token → replacement entries. The entries themselves are the
///     referential-integrity map consumed during redaction; these endpoints let an operator inspect and prune them.
/// </summary>
public static class ContextEndpoints
{
    public static void MapContextEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/contexts").WithTags("Contexts");

        group.MapGet("/", (MongoValkeyContextService contexts) => Results.Ok(contexts.ListContexts()))
            .WithName("ListContexts")
            .WithSummary("List all known context names.");

        group.MapPost("/{name}", (string name, MongoValkeyContextService contexts) =>
        {
            contexts.CreateContext(name);
            return Results.Ok();
        })
        .WithName("CreateContext")
        .WithSummary("Register a context.");

        group.MapGet("/{name}/entries", (string name, MongoValkeyContextService contexts) =>
            Results.Ok(contexts.GetEntries(name)))
        .WithName("GetContextEntries")
        .WithSummary("Get the token → replacement entries for a context.");

        group.MapDelete("/{name}", (string name, MongoValkeyContextService contexts) =>
        {
            contexts.DeleteContext(name);
            return Results.NoContent();
        })
        .WithName("DeleteContext")
        .WithSummary("Delete a context and all of its entries.");

        group.MapDelete("/{name}/entries/{token}", (string name, string token, MongoValkeyContextService contexts) =>
        {
            contexts.DeleteEntry(name, token);
            return Results.NoContent();
        })
        .WithName("DeleteContextEntry")
        .WithSummary("Delete a single token mapping from a context.");
    }
}
