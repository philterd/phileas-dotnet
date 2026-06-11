# Span Disambiguation

Different filters can detect the **same text at the same location** but classify it as different PII types. For example, a bare nine-digit number can be matched by both the SSN filter and a custom nine-digit identifier filter. **Span disambiguation** resolves these conflicts by looking at the surrounding context and choosing the most likely type.

It is a lightweight, context-learning technique (no machine-learning model or external data): the words around each unambiguous detection are hashed into a per–filter-type vector, and an ambiguous span is resolved to the candidate type whose accumulated vector its context most closely matches (by cosine similarity).

Span disambiguation is **disabled by default**.

## How It Works

1. **Training** — when only one filter claims a span (it is unambiguous), the words in its context window are hashed (MurmurHash3) into a vector accumulated under that filter type, within the span's context.
2. **Resolution** — when several filters claim the same location, the ambiguous span's context window is hashed and compared, by cosine similarity, against each candidate type's accumulated vector. The highest-scoring type wins; ties and cold starts fall back to the first candidate deterministically.

Disambiguation runs inside the pipeline **before** overlapping spans are dropped, so the winning type survives overlap resolution.

## Enabling It

Build a disambiguation service and pass it to `FilterService`:

```csharp
using Phileas.Services;
using Phileas.Services.Disambiguation;
using Phileas.Services.Disambiguation.Vector;

var options = new SpanDisambiguationOptions { Enabled = true };
var vectorStore = new InMemoryVectorService();
var disambiguation = SpanDisambiguationServiceFactory.Create(options, vectorStore);

var filterService = new FilterService(
    incrementalRedactionsEnabled: false,
    disambiguationService: disambiguation);

// Reuse the same filterService (and its vector store) across documents so it
// learns: unambiguous detections train the store, ambiguous ones are resolved.
var result = filterService.Filter(policy, "ctx", 0, input);
```

## Options

`SpanDisambiguationOptions`:

| Property | Default | Description |
|---|---|---|
| `Enabled` | `false` | Master switch. When `false`, the factory returns a no-op service that leaves spans untouched. |
| `VectorSize` | `512` | Size of the hash table backing each vector. **Immutable for a trained store** — it factors into the hash, so changing it invalidates persisted vectors. |
| `IgnoreStopWords` | `true` | Exclude common words from the context vectors. |
| `HashAlgorithm` | `"murmur3"` | Token hashing algorithm. `"murmur3"` (recommended) hashes UTF-8 bytes deterministically; any other value uses a deterministic string-hash fallback. |
| `StopWords` | (≈545 English words) | Comma-separated stop-word list used when `IgnoreStopWords` is `true`. |

## Persisting What It Learns

`InMemoryVectorService` keeps vectors in memory only; they are lost when the process exits. To make the learning survive restarts, use `FileBasedVectorService` and call `Save()` (or dispose it):

```csharp
using (var vectorStore = new FileBasedVectorService("vectors.json", vectorSize: 512, hashAlgorithm: "murmur3"))
{
    var disambiguation = SpanDisambiguationServiceFactory.Create(
        new SpanDisambiguationOptions { Enabled = true }, vectorStore);

    var filterService = new FilterService(false, disambiguation);
    // ... process documents ...
}   // Dispose() persists the accumulated vectors to vectors.json
```

The file records the vector size and hash algorithm it was built with. A store loaded under a **different** vector size or hash algorithm (or an unknown format version) is discarded and treated as a cold start, because its indexes would no longer be meaningful.

## See Also

- [Supported Identifiers](supported-identifiers.md) — the filters whose detections can compete
- [API Reference](api-reference.md) — `FilterService` constructors
