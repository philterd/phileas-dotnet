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
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Rest.Storage;

/// <summary>
///     Persists redaction policies in MongoDB as their canonical Phileas policy JSON, keyed by name. Policies
///     are validated (round-tripped through <see cref="PolicySerializer" />) on write, and on read the
///     configured local GLiNER model path is injected into any PhEye configuration that does not already
///     specify one — so authored policies stay portable across environments.
/// </summary>
public sealed class PolicyRepository
{
    private readonly IMongoCollection<PolicyDocument> _policies;
    private readonly string _modelPath;

    public PolicyRepository(IMongoDatabase database, PhileasRestOptions options)
    {
        _policies = database.GetCollection<PolicyDocument>("policies");
        _modelPath = options.PhEyeModelPath;
    }

    public void EnsureIndexes() =>
        _policies.Indexes.CreateOne(new CreateIndexModel<PolicyDocument>(
            Builders<PolicyDocument>.IndexKeys.Ascending(p => p.Name),
            new CreateIndexOptions { Unique = true, Name = "policy_name_unique" }));

    public IReadOnlyList<string> List() =>
        _policies.Find(FilterDefinition<PolicyDocument>.Empty)
            .Project(p => p.Name)
            .ToList();

    /// <summary>Returns the stored policy JSON for <paramref name="name" />, or <see langword="null" /> if absent.</summary>
    public string? GetJson(string name) =>
        _policies.Find(p => p.Name == name)
            .Project(p => p.Json)
            .FirstOrDefault();

    /// <summary>
    ///     Loads and deserializes the named policy ready to filter with, with its <see cref="Policy.Name" /> set and
    ///     the container's GLiNER model path injected. Returns <see langword="null" /> if the policy does not exist.
    /// </summary>
    public PhileasPolicy? Load(string name)
    {
        var json = GetJson(name);
        if (json == null)
            return null;

        var policy = PolicySerializer.DeserializeFromJson(json);
        policy.Name = name;
        InjectModelPath(policy);
        return policy;
    }

    /// <summary>
    ///     Validates <paramref name="json" /> (throwing <see cref="ArgumentException" /> if it is not a valid policy)
    ///     and upserts it under <paramref name="name" />.
    /// </summary>
    public void Save(string name, string json)
    {
        // Round-trip to validate; keep the caller's canonical JSON as stored.
        _ = PolicySerializer.DeserializeFromJson(json);

        _policies.ReplaceOne(
            p => p.Name == name,
            new PolicyDocument { Name = name, Json = json },
            new ReplaceOptions { IsUpsert = true });
    }

    /// <summary>Deletes the named policy. Returns <see langword="true" /> if a policy was removed.</summary>
    public bool Delete(string name) =>
        _policies.DeleteOne(p => p.Name == name).DeletedCount > 0;

    /// <summary>
    ///     Sets the configured local model path on any PhEye configuration that has not already specified one, so
    ///     detection runs against the in-container GLiNER model. A policy that explicitly sets its own modelPath (or
    ///     a remote endpoint) is left untouched.
    /// </summary>
    private void InjectModelPath(PhileasPolicy policy)
    {
        if (string.IsNullOrEmpty(_modelPath) || policy.Identifiers.PhEyes == null)
            return;

        foreach (var phEye in policy.Identifiers.PhEyes)
        {
            if (phEye.PhEyeConfiguration != null && string.IsNullOrEmpty(phEye.PhEyeConfiguration.ModelPath))
                phEye.PhEyeConfiguration.ModelPath = _modelPath;
        }
    }

    [BsonIgnoreExtraElements]
    private sealed class PolicyDocument
    {
        [BsonId] public ObjectId Id { get; set; }
        [BsonElement("name")] public string Name { get; set; } = string.Empty;
        [BsonElement("json")] public string Json { get; set; } = string.Empty;
    }
}
