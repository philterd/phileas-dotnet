# Context Service and Referential Integrity

## Overview

When the `RANDOM_REPLACE` filter strategy is used, Phileas replaces detected PII with a realistic, type-appropriate fake value. By default (`replacementScope = "DOCUMENT"`), each occurrence is anonymized independently, so the **same PII token appearing multiple times** can map to **different** fake values.

When you need referential integrity, meaning the same input value always maps to the same fake value, set `replacementScope = "CONTEXT"` on the strategy. In that mode the **Context Service** maintains a mapping of PII tokens to their replacement values within a named *context*: if the same token is encountered again inside the same context, the previously-generated replacement is reused.

> **In short:** the Context Service only affects `RANDOM_REPLACE` strategies, and a date `SHIFT` with `shiftRandom`, whose `replacementScope` is `"CONTEXT"`. With the default `"DOCUMENT"` scope the context service is not consulted.

## Concepts

### Context

A *context* is a named scope that groups related filter operations. For example, all documents belonging to the same patient could share a context named `"patient-123"`. With `replacementScope = "CONTEXT"`, the SSN `123-45-6789` will always be replaced with the same fake value within that context, regardless of how many times it appears.

### Referential Integrity

Referential integrity means that the relationship between two pieces of data is preserved after filtering. For example, if a report and a database record both reference the same SSN, a consistent random replacement ensures that the anonymised copies still refer to the same (now-fictional) identity.

## IContextService Interface

```csharp
namespace Phileas.Services;

public interface IContextService
{
    /// Returns the stored replacement for the given token in the context,
    /// or null if no replacement has been stored yet.
    string? Get(string contextName, string token);

    /// Stores a replacement value for the given token in the context.
    void Put(string contextName, string token, string replacement);

    /// Returns a value that belongs to the context rather than to a token,
    /// creating and storing one only if the context has none for the key yet.
    /// The default implementation uses Get and Put.
    string ComputeValueIfAbsent(string contextName, string key, FilterType filterType,
        Func<string> valueFactory);
}
```

Implementations are free to persist the context map anywhere (in-memory, a database, a distributed cache, etc.).

## Default Implementation: InMemoryContextService

`InMemoryContextService` (in `Phileas.Services`) is the default implementation. It stores all context maps in a thread-safe, in-process dictionary. The mappings are lost when the process exits.

```csharp
var contextService = new InMemoryContextService();
```

This is the implementation used automatically by `FilterService` when no `IContextService` is supplied.

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
// FilterService defaults to InMemoryContextService.
var result = new FilterService().Filter(
    policy,
    context: "patient-123",
    piece: 0,
    input: "SSN: 123-45-6789"
);
```

### Supplying a custom context service

```csharp
IContextService myContextService = new MyDatabaseContextService(connectionString);

var result = new FilterService().Filter(
    policy,
    context: "patient-123",
    piece: 0,
    input: "SSN: 123-45-6789",
    contextService: myContextService
);
```

### Using RANDOM_REPLACE directly on a filter strategy

This uses the **runtime** strategy types (in `Phileas.Filters.*`), which carry the `ContextService`. The
policy-level strategy types (in `Phileas.Policy.Filters.Strategies`) do not carry it, so `FilterService` wires the
context service onto the runtime strategies for you.

```csharp
using Phileas.Filters;                  // runtime AbstractFilterStrategy
using Phileas.Filters.Strategies.Rules; // runtime SsnFilterStrategy
using Phileas.Services;                 // InMemoryContextService

var contextService = new InMemoryContextService();

var strategy = new SsnFilterStrategy
{
    Strategy = AbstractFilterStrategy.RandomReplace,
    ReplacementScope = AbstractFilterStrategy.ReplacementScopeContext,  // "CONTEXT"
    ContextService = contextService
};
```

## How It Works

1. The `RANDOM_REPLACE` branch in `StandardFilterStrategy` calls `GetOrCreateRandomReplacement(context, token)`.
2. If `ReplacementScope` is `"CONTEXT"` **and** a `ContextService` is set, the method calls `ContextService.Get(context, token)`.
   - **Hit**: the previously stored replacement is returned unchanged.
   - **Miss**: a new replacement is generated (a realistic fake value via the anonymization service, or a GUID when none is wired in), stored via `ContextService.Put(context, token, replacement)`, and returned.
3. Otherwise (the default `"DOCUMENT"` scope, or no `ContextService`), a fresh replacement is generated for every occurrence and the context service is not consulted.

## Values kept with a context

Some values belong to a context rather than to a token. A date `SHIFT` with `shiftRandom` and `replacementScope = "CONTEXT"` draws one random shift for the context and moves every date in it by that many days (see [Random shift in a context](filter-strategies.md#random-shift-in-a-context)). It keeps the shift through `ComputeValueIfAbsent(context, "\0date-shift", FilterType.Date, ...)`, as a number of days.

The default implementation of `ComputeValueIfAbsent` stores the value as a token entry, with the key as the token, through `Get` and `Put`. A detected token cannot contain a NUL character, so the key cannot be a token. The default is not atomic: if two dates in a new context are shifted at the same time, both can find no shift and each store one, so they can move by different amounts. `InMemoryContextService` overrides it so the value is created once. Override it in your own implementation if it is used from more than one thread or process, and keep the value with the context itself if your implementation evicts, deletes or lists a context's entries.

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

Pass an instance of your implementation to `FilterService.Filter` via the `contextService` parameter.
