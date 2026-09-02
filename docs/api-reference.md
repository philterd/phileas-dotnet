# API Reference

## FilterService

`FilterService` (in `Phileas.Services`) is the main entry point. It implements the `IFilterService` interface.

### Constructors

```csharp
public FilterService();
public FilterService(bool incrementalRedactionsEnabled);
public FilterService(bool incrementalRedactionsEnabled, ISpanDisambiguationService disambiguationService);
```

| Parameter | Description |
|---|---|
| `incrementalRedactionsEnabled` | When `true`, each `TextFilterResult` carries a per-redaction snapshot trail in `IncrementalRedactions`. |
| `disambiguationService` | Resolves spans that compete at the same location by surrounding context. Defaults to a no-op. See [Span Disambiguation](span-disambiguation.md). |

### Filter

```csharp
public TextFilterResult Filter(
    Policy policy,
    string context,
    int piece,
    string input,
    IContextService? contextService = null)
```

Applies the policy to `input` and returns a [`TextFilterResult`](#textfilterresult).

| Parameter | Type | Description |
|---|---|---|
| `policy` | `Policy` | The policy defining which identifiers to detect and how to replace them. |
| `context` | `string` | A named scope used by `RANDOM_REPLACE` to maintain referential integrity. |
| `piece` | `int` | Zero-based index of the text fragment within a larger document (recorded in each `Span`). |
| `input` | `string` | The text to filter. |
| `contextService` | `IContextService?` | Optional context service. Defaults to `InMemoryContextService` when `null`. |

**Returns:** [`TextFilterResult`](#textfilterresult)

**Example:**

```csharp
var filterService = new FilterService();
var result = filterService.Filter(
    policy,
    context: "patient-123",
    piece: 0,
    input: "SSN: 123-45-6789"
);
```

---

### Evaluate

```csharp
public EvaluationResult Evaluate(
    Policy policy,
    string context,
    int piece,
    string input,
    IList<Span> groundTruthSpans,
    IContextService? contextService = null)
```

Filters `input` using the policy, then compares the detected [`Span`](#span) objects against `groundTruthSpans` and returns precision, recall, and F1 metrics.

A detected span is counted as a **true positive** when its `CharacterStart` and `CharacterEnd` exactly match those of a ground-truth span. Spans that are detected but not in the ground truth are **false positives**; ground-truth spans that were not detected are **false negatives**.

| Parameter | Type | Description |
|---|---|---|
| `policy` | `Policy` | The policy defining which identifiers to detect and how to replace them. |
| `context` | `string` | A named scope used by `RANDOM_REPLACE` to maintain referential integrity. |
| `piece` | `int` | Zero-based index of the text fragment within a larger document. |
| `input` | `string` | The text to filter and evaluate against. |
| `groundTruthSpans` | `IList<Span>` | The expected set of spans (ground truth). Only `CharacterStart` and `CharacterEnd` are used for matching. |
| `contextService` | `IContextService?` | Optional context service. Defaults to `InMemoryContextService` when `null`. |

**Returns:** [`EvaluationResult`](#evaluationresult)

**Example:**

```csharp
var filterService = new FilterService();
var groundTruth = new List<Span>
{
    new Span { CharacterStart = 8, CharacterEnd = 28 }  // position of "john.doe@example.com"
};

var evaluation = filterService.Evaluate(
    policy,
    context: "default",
    piece: 0,
    input: "Contact john.doe@example.com for help.",
    groundTruthSpans: groundTruth
);

Console.WriteLine($"Precision: {evaluation.Precision:F2}");
Console.WriteLine($"Recall:    {evaluation.Recall:F2}");
Console.WriteLine($"F1:        {evaluation.F1:F2}");
```

---

## EvaluationResult

```csharp
public class EvaluationResult
{
    public double Precision { get; }
    public double Recall { get; }
    public double F1 { get; }
}
```

Returned by [`IFilterService.Evaluate`](#evaluate). All values are in the range [0, 1].

| Property | Type | Description |
|---|---|---|
| `Precision` | `double` | Fraction of detected spans that are correct: `TP / (TP + FP)`. Returns `0` when no spans were detected. |
| `Recall` | `double` | Fraction of ground-truth spans that were detected: `TP / (TP + FN)`. Returns `0` when ground truth is empty. |
| `F1` | `double` | Harmonic mean of precision and recall: `2 × Precision × Recall / (Precision + Recall)`. Returns `0` when both are zero. |

---

## TextFilterResult

```csharp
public class TextFilterResult
{
    public string FilteredText { get; }
    public string Context { get; }
    public int Piece { get; }
    public IList<Span> Spans { get; }
    public IList<IncrementalRedaction> IncrementalRedactions { get; }
    public long Tokens { get; }
}
```

| Property | Type | Description |
|---|---|---|
| `FilteredText` | `string` | The input text with all detected PII replaced according to the active filter strategies. |
| `Context` | `string` | The context name passed to `Filter`. |
| `Piece` | `int` | The piece index passed to `Filter`. |
| `Spans` | `IList<Span>` | Ordered list of [`Span`](#span) objects describing each detected PII occurrence. |
| `IncrementalRedactions` | `IList<IncrementalRedaction>` | Per-redaction snapshot trail (populated only when the service is constructed with incremental redactions enabled). |
| `Tokens` | `long` | Number of whitespace-delimited tokens in the input. |

---

## Span

A `Span` describes a single detected PII occurrence within the input text.

```csharp
public class Span
{
    public int CharacterStart { get; set; }
    public int CharacterEnd { get; set; }
    public FilterType FilterType { get; set; }
    public string Context { get; set; }
    public string? Classification { get; set; }
    public double Confidence { get; set; }
    public string Text { get; set; }
    public string Replacement { get; set; }
    public string Salt { get; set; }
    public bool Ignored { get; set; }
    public bool Applied { get; set; }
    public int Priority { get; set; }
}
```

| Property | Type | Description |
|---|---|---|
| `CharacterStart` | `int` | Zero-based index of the first character of the detected token in the original input. |
| `CharacterEnd` | `int` | Zero-based index of the character immediately after the detected token. |
| `FilterType` | `FilterType` | The type of PII detected (e.g. `FilterType.Ssn`, `FilterType.EmailAddress`). |
| `Context` | `string` | The context name passed to `FilterService.Filter`. |
| `Classification` | `string?` | Optional sub-classification for the token (filter-type specific). |
| `Confidence` | `double` | Confidence score (0–1) assigned by the detection pattern. |
| `Text` | `string` | The original detected token text. |
| `Replacement` | `string` | The value that replaces the token in `FilteredText`. |
| `Salt` | `string` | The random salt used during hashing (empty if salt was not enabled). |
| `Ignored` | `bool` | `true` if the token was detected but excluded by an ignore rule. |
| `Applied` | `bool` | `true` if the replacement was applied (i.e. the token was modified in the output). |
| `Priority` | `int` | The priority of the filter that produced this span. |

### Static Helpers

```csharp
// Create a new Span
Span.Make(characterStart, characterEnd, filterType, context, confidence,
          text, replacement, salt, ignored, applied, window, priority);

// Check whether a span with the same bounds already exists in a list
Span.DoesSpanExist(characterStart, characterEnd, spans);

// Resolve overlapping spans, keeping the longest (then highest confidence,
// then highest priority, then earliest) and discarding the rest
IList<Span> Span.DropOverlappingSpans(IList<Span> spans);
```

---

## FilterType

`FilterType` (in `Phileas.Model`) is an enum that identifies the category of detected PII. Its values and their `GetFilterTypeName()` strings:

| Value | `GetFilterTypeName()` |
|---|---|
| `Age` | `"age"` |
| `BankRoutingNumber` | `"bank-routing-number"` |
| `BitcoinAddress` | `"bitcoin-address"` |
| `Currency` | `"currency"` |
| `CreditCard` | `"credit-card"` |
| `DriversLicenseNumber` | `"drivers-license-number"` |
| `Ein` | `"ein"` |
| `LocationCity` | `"city"` |
| `LocationState` | `"state"` |
| `LocationCounty` | `"county"` |
| `Date` | `"date"` |
| `EmailAddress` | `"email-address"` |
| `FirstName` | `"first-name"` |
| `Hospital` | `"hospital"` |
| `HospitalAbbreviation` | `"hospital-abbreviation"` |
| `IbanCode` | `"iban-code"` |
| `Identifier` | `"identifier"` |
| `IpAddress` | `"ip-address"` |
| `MacAddress` | `"mac-address"` |
| `PassportNumber` | `"passport-number"` |
| `PhEye` | `"ph-eye"` |
| `PhoneNumber` | `"phone-number"` |
| `PhoneNumberExtension` | `"phone-number-extension"` |
| `Section` | `"section"` |
| `Ssn` | `"ssn"` |
| `StateAbbreviation` | `"state-abbreviation"` |
| `StreetAddress` | `"street-address"` |
| `Surname` | `"surname"` |
| `TrackingNumber` | `"tracking-number"` |
| `Url` | `"url"` |
| `Vin` | `"vin"` |
| `ZipCode` | `"zip-code"` |
| `CustomDictionary` | `"custom-dictionary"` |
| `Dictionary` | `"dictionary"` |
| `Person` | `"person"` |
| `MedicalCondition` | `"medical-condition"` |
| `Other` | `"other"` |

Call `filterType.GetFilterTypeName()` to get the lower-kebab-case string used in redaction format tokens (e.g. `"email-address"`, `"ssn"`).

---

## IContextService

```csharp
namespace Phileas.Services;

public interface IContextService
{
    string? Get(string contextName, string token);
    void Put(string contextName, string token, string replacement);
}
```

Implement this interface to persist random replacement mappings across calls or processes. See [Context Service](context-service.md) for a full explanation and examples.

---

## InMemoryContextService

```csharp
var contextService = new InMemoryContextService();
```

The default implementation of `IContextService` (in `Phileas.Services`). Stores all mappings in a thread-safe in-process dictionary. Mappings are lost when the process exits.

---

## Policy

```csharp
public class Policy
{
    public string Name { get; set; }        // in-memory label only; [JsonIgnore]
    public Config Config { get; set; }
    public Crypto? Crypto { get; set; }
    public Fpe? Fpe { get; set; }
    public Identifiers Identifiers { get; set; }
    public List<Ignored> Ignored { get; set; }
    public List<IgnoredPattern> IgnoredPatterns { get; set; }
    public Graphical Graphical { get; set; }
}
```

Post-filter cleanup lives on `Config.PostFilters` (not at the top level). Use `PolicySerializer.SerializeToJson` / `DeserializeFromJson` to convert to and from JSON. See [Policies](policies.md) for full documentation.

---

## FilterConfiguration and Builder

`FilterConfiguration` carries the settings used to construct an individual filter. Use `FilterConfiguration.Builder` when building filters outside of `FilterService`:

```csharp
var config = new FilterConfiguration.Builder()
    .WithStrategies(strategies)
    .WithIgnored(ignoredSet)
    .WithIgnoredPatterns(ignoredPatterns)
    .WithWindowSize(5)
    .WithPriority(0)
    .Build();
```

| Builder Method | Description |
|---|---|
| `WithStrategies(IList<AbstractFilterStrategy>)` | Sets the list of filter strategies. |
| `WithIgnored(ISet<string>)` | Sets the set of exact values to ignore. |
| `WithIgnoredFiles(ISet<string>)` | Sets a set of file-based ignore lists (reserved). |
| `WithIgnoredPatterns(IList<IgnoredPattern>)` | Sets the list of ignored regex patterns. |
| `WithCrypto(Crypto?)` | Provides the AES key for `CRYPTO_REPLACE`. |
| `WithFpe(Fpe?)` | Provides FPE key/tweak for `FPE_ENCRYPT_REPLACE`. |
| `WithWindowSize(int)` | Sets the context window size. |
| `WithPriority(int)` | Sets the filter priority. |
| `WithPostFilters(Policy.PostFilters?)` | Sets the post-filter cleanup options. |
| `WithRegexTimeoutMs(long)` | Sets the per-pattern regex match timeout (used by custom regex filters). |

---

## AbstractFilterStrategy (runtime)

`Phileas.Filters.AbstractFilterStrategy` is the runtime base class for all filter strategies. It carries the resolved settings used at filter time:

| Property | Type | Default | Description |
|---|---|---|---|
| `Strategy` | `string` | `"REDACT"` | Strategy name constant. |
| `RedactionFormat` | `string` | `"{{{REDACTED-%t}}}"` | Redaction label template. |
| `StaticReplacement` | `string` | `""` | Value used by `STATIC_REPLACE`. |
| `MaskCharacter` | `string` | `"*"` | Character used by `MASK`. |
| `MaskLength` | `string` | `"same"` | Mask length (`"same"` or integer string). |
| `Condition` | `string?` | `null` | Condition expression evaluated to decide whether the strategy applies (see [Filter Conditions](filter-conditions.md)). |
| `Salt` | `bool` | `false` | Whether to add a random salt before hashing. |
| `ContextService` | `IContextService?` | `null` | Context service for `RANDOM_REPLACE` under `CONTEXT` scope. |
| `AnonymizationService` | `IAnonymizationService?` | `null` | Generates realistic fake values for `RANDOM_REPLACE`. |
| `AnonymizationMethod` | `string?` | `null` | Generation method for `RANDOM_REPLACE`. |
| `AnonymizationCandidates` | `List<string>?` | `null` | Explicit value pool for `RANDOM_REPLACE`. |
| `ReplacementScope` | `string` | `"DOCUMENT"` | `"DOCUMENT"` (fresh each occurrence) or `"CONTEXT"` (reuse per token). |
| `Mappings` | `Dictionary<string,string>?` | `null` | `MAP_REPLACE` inline lookup table (overrides `MappingFiles`). |
| `MappingFiles` | `List<string>?` | `null` | `MAP_REPLACE` tab-separated lookup-table files. |
| `CaseSensitive` | `bool` | `false` | `MAP_REPLACE` whether lookup keys are matched case-sensitively. |
| `Generator` | `string?` | `null` | `MAP_REPLACE` name of a generator in the policy's `generators` block. |
| `FallbackStrategy` | `string` | `"REDACT"` | `MAP_REPLACE` strategy applied when the token is unmapped and no generator produces a value. |
