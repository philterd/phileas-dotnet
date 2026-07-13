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

/// <summary>CRUD endpoints for redaction policies stored in MongoDB.</summary>
public static class PolicyEndpoints
{
    public static void MapPolicyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/policies").WithTags("Policies");

        group.MapGet("/", (PolicyRepository policies) => Results.Ok(policies.List()))
            .WithName("ListPolicies")
            .WithSummary("List the names of all stored policies.");

        group.MapGet("/{name}", (string name, PolicyRepository policies) =>
        {
            var json = policies.GetJson(name);
            return json == null
                ? Results.NotFound($"Policy '{name}' not found.")
                : Results.Content(json, "application/json");
        })
        .WithName("GetPolicy")
        .WithSummary("Get a policy's canonical JSON.");

        group.MapPut("/{name}", (string name, PolicyUpsertRequest request, PolicyRepository policies) =>
        {
            try
            {
                policies.Save(name, request.Json);
                return Results.Ok();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest($"Invalid policy: {ex.Message}");
            }
        })
        .WithName("UpsertPolicy")
        .WithSummary("Create or replace a policy. The body is validated as canonical Phileas policy JSON.");

        group.MapDelete("/{name}", (string name, PolicyRepository policies) =>
            policies.Delete(name) ? Results.NoContent() : Results.NotFound($"Policy '{name}' not found."))
        .WithName("DeletePolicy")
        .WithSummary("Delete a policy.");
    }
}
