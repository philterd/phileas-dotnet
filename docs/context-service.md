# Context Service and Referential Integrity

## Overview

When the `RANDOM_REPLACE` filter strategy is used, Phileas replaces detected PII with a randomly-generated value. Without any additional bookkeeping, the **same PII token appearing multiple times** in different documents (or even in the same document) would be replaced by **different random values**, breaking referential integrity.

The **Context Service** solves this problem. It maintains a mapping of PII tokens to their replacement values within a named *context*. If the same token is encountered again inside the same context, the previously-generated replacement is reused, ensuring consistency across the filtered output.

## Concepts

### Context

A *context* is a named scope that groups related filter operations. For example, all documents belonging to the same patient could share a context named `"patient-123"`. Within that context, the SSN `123-45-6789` will always be replaced with the same random value regardless of how many times it appears.

### Referential Integrity

Referential integrity means that the relationship between two pieces of data is preserved after filtering. For example, if a report and a database record both reference the same SSN, a consistent random replacement ensures that the anonymised copies still refer to the same (now-fictional) identity.

## IContextService Interface

```csharp
namespace Phileas.Filters;

public interface IContextService
{
    /// Returns the stored replacement for the given token in the context,
    /// or null if no replacement has been stored yet.
    string? Get(string contextName, string token);

    /// Stores a replacement value for the given token in the context.
    void Put(string contextName, string token, string replacement);
}
```

Implementations are free to persist the context map anywhere (in-memory, a database, a distributed cache, etc.).

## Default Implementation: InMemoryContextService

`InMemoryContextService` (in `Phileas.Services`) is the default implementation. It stores all context maps in a thread-safe, in-process dictionary. The mappings are lost when the process exits.

```csharp
var contextService = new InMemoryContextService();
```

This is the implementation used automatically by `FilterPolicyLoader` when no `IContextService` is supplied.

## Usage

### Using the default (in-memory) context service

```csharp
var policy = new Policy
{
    Name = "my-policy",
    Identifiers = new Identifiers
    {
        Ssn = new Ssn()
    }
};

// RANDOM_REPLACE strategy is set on the policy filter strategy.
// FilterPolicyLoader defaults to InMemoryContextService.
var result = FilterPolicyLoader.Filter(
    new List<Policy> { policy },
    context: "patient-123",
    piece: 0,
    input: "SSN: 123-45-6789"
);
```

### Supplying a custom context service

```csharp
IContextService myContextService = new MyDatabaseContextService(connectionString);

var result = FilterPolicyLoader.Filter(
    new List<Policy> { policy },
    context: "patient-123",
    piece: 0,
    input: "SSN: 123-45-6789",
    contextService: myContextService
);
```

### Using RANDOM_REPLACE directly on a filter strategy

```csharp
var contextService = new InMemoryContextService();

var strategy = new SsnFilterStrategy
{
    Strategy = AbstractFilterStrategy.RandomReplace,
    ContextService = contextService
};
```

## How It Works

1. The `RANDOM_REPLACE` branch in `StandardFilterStrategy` calls `GetOrCreateRandomReplacement(context, token)`.
2. If a `ContextService` is set, the method calls `ContextService.Get(context, token)`.
   - **Hit**: the previously stored random value is returned unchanged.
   - **Miss**: a new `Guid` is generated, stored via `ContextService.Put(context, token, guid)`, and returned.
3. If no `ContextService` is set (strategy used outside of `FilterPolicyLoader`), a fresh `Guid` is generated each time with no persistence.

## Implementing a Custom Context Service

To share replacement values across processes or persist them between runs, implement `IContextService`:

```csharp
public class RedisContextService : IContextService
{
    private readonly IDatabase _db;

    public RedisContextService(IDatabase redisDatabase)
    {
        _db = redisDatabase;
    }

    public string? Get(string contextName, string token)
    {
        var value = _db.HashGet(contextName, token);
        return value.IsNull ? null : (string?)value;
    }

    public void Put(string contextName, string token, string replacement)
    {
        _db.HashSet(contextName, token, replacement);
    }
}
```

Pass an instance of your implementation to `FilterPolicyLoader.Filter` via the `contextService` parameter.
