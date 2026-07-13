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
using Phileas.Services;
using StackExchange.Redis;

namespace Phileas.Rest.Storage;

/// <summary>
///     <see cref="IContextService" /> backed by MongoDB (durable source of truth) fronted by a Valkey cache.
///     <para>
///         Referential integrity for RANDOM_REPLACE lives in the <c>context_entries</c> collection: a
///         <c>(context, token)</c> pair maps to exactly one replacement. <see cref="Get" /> is cache-aside
///         (Valkey first, then Mongo, repopulating the cache on a miss); <see cref="Put" /> writes through to
///         Mongo first — so a Valkey eviction or flush never loses a mapping — then updates the cache.
///     </para>
///     <para>
///         Registered as a singleton. The MongoDB client and the Redis <see cref="IConnectionMultiplexer" /> are
///         both thread-safe and use genuine synchronous APIs, so the synchronous <see cref="IContextService" />
///         contract needs no sync-over-async.
///     </para>
/// </summary>
public sealed class MongoValkeyContextService : IContextService
{
    private readonly IMongoCollection<ContextEntryDocument> _entries;
    private readonly IMongoCollection<ContextDocument> _contexts;
    private readonly IDatabase? _cache;
    private readonly TimeSpan? _cacheTtl;

    public MongoValkeyContextService(IMongoDatabase database, IConnectionMultiplexer? redis, int cacheTtlSeconds)
    {
        _entries = database.GetCollection<ContextEntryDocument>("context_entries");
        _contexts = database.GetCollection<ContextDocument>("contexts");
        _cache = redis?.GetDatabase();
        _cacheTtl = cacheTtlSeconds > 0 ? TimeSpan.FromSeconds(cacheTtlSeconds) : null;
    }

    /// <summary>Creates the unique indexes the service relies on. Safe to call repeatedly (idempotent).</summary>
    public void EnsureIndexes()
    {
        // One replacement per (context, token) — makes Put an upsert and Get an indexed point lookup.
        _entries.Indexes.CreateOne(new CreateIndexModel<ContextEntryDocument>(
            Builders<ContextEntryDocument>.IndexKeys.Ascending(e => e.Context).Ascending(e => e.Token),
            new CreateIndexOptions { Unique = true, Name = "context_token_unique" }));
    }

    /// <inheritdoc />
    public string? Get(string contextName, string token)
    {
        if (_cache != null)
        {
            var cached = _cache.HashGet(CacheKey(contextName), token);
            if (cached.HasValue)
                return cached!;
        }

        var replacement = _entries
            .Find(e => e.Context == contextName && e.Token == token)
            .Project(e => e.Replacement)
            .FirstOrDefault();

        if (replacement != null && _cache != null)
            WriteCache(contextName, token, replacement);

        return replacement;
    }

    /// <inheritdoc />
    public void Put(string contextName, string token, string replacement)
    {
        // Mongo is authoritative: write it first so a cache failure can never lose referential integrity.
        _entries.UpdateOne(
            e => e.Context == contextName && e.Token == token,
            Builders<ContextEntryDocument>.Update
                .Set(e => e.Replacement, replacement)
                .SetOnInsert(e => e.Context, contextName)
                .SetOnInsert(e => e.Token, token),
            new UpdateOptions { IsUpsert = true });

        // Register the context lazily so it appears in the management API without an explicit create call.
        _contexts.UpdateOne(
            c => c.Name == contextName,
            Builders<ContextDocument>.Update.SetOnInsert(c => c.Name, contextName),
            new UpdateOptions { IsUpsert = true });

        if (_cache != null)
            WriteCache(contextName, token, replacement);
    }

    // ---- Context management (used by the /contexts endpoints) ----

    public IReadOnlyList<string> ListContexts() =>
        _contexts.Find(FilterDefinition<ContextDocument>.Empty)
            .Project(c => c.Name)
            .ToList();

    public void CreateContext(string contextName) =>
        _contexts.UpdateOne(
            c => c.Name == contextName,
            Builders<ContextDocument>.Update.SetOnInsert(c => c.Name, contextName),
            new UpdateOptions { IsUpsert = true });

    /// <summary>Deletes a context and all of its entries, and drops its cache entry.</summary>
    public void DeleteContext(string contextName)
    {
        _entries.DeleteMany(e => e.Context == contextName);
        _contexts.DeleteOne(c => c.Name == contextName);
        _cache?.KeyDelete(CacheKey(contextName));
    }

    public IReadOnlyDictionary<string, string> GetEntries(string contextName) =>
        _entries.Find(e => e.Context == contextName)
            .ToList()
            .ToDictionary(e => e.Token, e => e.Replacement);

    /// <summary>Removes a single token mapping from a context and invalidates it in the cache.</summary>
    public void DeleteEntry(string contextName, string token)
    {
        _entries.DeleteOne(e => e.Context == contextName && e.Token == token);
        _cache?.HashDelete(CacheKey(contextName), token);
    }

    private void WriteCache(string contextName, string token, string replacement)
    {
        var key = CacheKey(contextName);
        _cache!.HashSet(key, token, replacement);
        if (_cacheTtl.HasValue)
            _cache.KeyExpire(key, _cacheTtl);
    }

    private static RedisKey CacheKey(string contextName) => $"phileas:ctx:{contextName}";

    [BsonIgnoreExtraElements]
    private sealed class ContextEntryDocument
    {
        [BsonId] public ObjectId Id { get; set; }
        [BsonElement("context")] public string Context { get; set; } = string.Empty;
        [BsonElement("token")] public string Token { get; set; } = string.Empty;
        [BsonElement("replacement")] public string Replacement { get; set; } = string.Empty;
    }

    [BsonIgnoreExtraElements]
    private sealed class ContextDocument
    {
        [BsonId] public ObjectId Id { get; set; }
        [BsonElement("name")] public string Name { get; set; } = string.Empty;
    }
}
