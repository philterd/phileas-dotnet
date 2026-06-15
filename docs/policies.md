# Policies

A **Policy** is the primary configuration object in phileas-dotnet. It defines which PII types to detect, how to handle each type, global settings, and values that should never be redacted.

## Policy Structure

```csharp
public class Policy
{
    public string Name { get; set; }                       // in-memory label only (not serialized)
    public Config Config { get; set; }
    public Crypto? Crypto { get; set; }
    public Fpe? Fpe { get; set; }
    public Identifiers Identifiers { get; set; }
    public List<Ignored> Ignored { get; set; }
    public List<IgnoredPattern> IgnoredPatterns { get; set; }
    public Graphical Graphical { get; set; }
}
```

> The canonical policy JSON has **no top-level `name`** — `Name` is an in-memory convenience label and is marked `[JsonIgnore]`. Use [`PolicySerializer`](#serializing-policies) to load and save policies; it applies the canonical options (null fields omitted) and resolves `${ENV_VAR}` / `env:NAME` placeholders.

### Equivalent JSON

```json
{
  "identifiers": {
    "ssn": {},
    "emailAddress": {}
  }
}
```

---

## Config

`Config` holds global settings, grouped into sub-objects.

| Property | Type | JSON key | Description |
|---|---|---|---|
| `Splitting` | `Splitting` | `splitting` | Splits long input into pieces before filtering. |
| `Pdf` | `Pdf` | `pdf` | PDF redaction rendering options. |
| `PostFilters` | `PostFilters` | `postFilters` | Cleanup applied to replacements (see [PostFilters](#postfilters)). |
| `Analysis` | `Analysis` | `analysis` | Analysis options. |

### Splitting

| Property | JSON key | Default | Description |
|---|---|---|---|
| `Enabled` | `enabled` | `false` | Enable splitting of long inputs. |
| `Threshold` | `threshold` | `10000` | Minimum input length (characters) before splitting applies. |
| `Method` | `method` | `"newline"` | Split method (e.g. `"newline"`). |

```csharp
var policy = new Policy
{
    Name = "my-policy",
    Config = new Config
    {
        Splitting = new Splitting { Enabled = true, Threshold = 5000, Method = "newline" }
    }
};
```

> The per-filter context **window size** is configured on each identifier via `WindowSize` (see [Common Options](#common-identifier-options)), not on `Config`.

---

## Crypto

`Crypto` provides the AES key used by the `CRYPTO_REPLACE` filter strategy, which encrypts with **AES-GCM**.

| Property | Type | JSON key | Description |
|---|---|---|---|
| `Key` | `string?` | `key` | **Hex-encoded** 16, 24, or 32-byte AES key, or an `env:NAME` reference. |
| `Iv` | `string?` | `iv` | Present in the model but unused by AES-GCM, which generates a fresh random nonce per value. |

```csharp
var policy = new Policy
{
    Name = "encrypted-policy",
    Crypto = new Crypto
    {
        Key = Convert.ToHexString(aesKey)   // hex-encoded AES key
    }
};
```

See [Filter Strategies — CRYPTO_REPLACE](filter-strategies.md#crypto_replace) for usage.

---

## Fpe

`Fpe` provides the key and tweak used by the `FPE_ENCRYPT_REPLACE` (Format Preserving Encryption, FF3-1) strategy.

| Property | Type | JSON key | Description |
|---|---|---|---|
| `Key` | `string?` | `key` | **Hex-encoded** FF3-1 key, or an `env:NAME` reference. |
| `Tweak` | `string?` | `tweak` | **Hex-encoded** tweak (required by FF3-1; 56- or 64-bit), or an `env:NAME` reference. |

See [Filter Strategies — FPE_ENCRYPT_REPLACE](filter-strategies.md#fpe_encrypt_replace).

---

## Identifiers

`Identifiers` lists which PII types the policy should detect. Set the corresponding property to a non-`null` value to enable that filter. See [Supported Identifiers](supported-identifiers.md) for the full list.

```csharp
var identifiers = new Identifiers
{
    Ssn           = new Ssn(),
    EmailAddress  = new EmailAddress(),
    PhoneNumber   = new PhoneNumber()
};
```

### Common Identifier Options

Each identifier class extends `AbstractPolicyFilter` and supports these common options:

| Property | JSON key | Type | Default | Description |
|---|---|---|---|---|
| `Enabled` | `enabled` | `bool` | `true` | Whether the filter is active. |
| `Ignored` | `ignored` | `List<string>?` | `null` | Exact values that should not be redacted. |
| `IgnoredFiles` | `ignoredFiles` | `List<string>?` | `null` | Files whose lines provide additional ignored terms. |
| `IgnoredPatterns` | `ignoredPatterns` | `List<IgnoredPattern>?` | `null` | Regex patterns whose matches are not redacted. |
| `WindowSize` | `windowSize` | `int` | `0` | Context words on each side of a match; `0` uses the default (5). |
| `Priority` | `priority` | `int` | `0` | Higher-priority filter spans win when spans overlap. |

---

## Custom Identifiers

The `identifiers.identifiers` array holds custom regex-based identifiers. Each detects values with a user-supplied pattern.

| Property | JSON key | Type | Default | Description |
|---|---|---|---|---|
| `Classification` | `classification` | `string` | `custom-identifier` | Label applied to matches (used as the filter type). |
| `Pattern` | `pattern` | `string` | `\b[A-Z0-9_-]{6,}\b` | The regular expression to match. |
| `CaseSensitive` | `caseSensitive` | `bool` | `true` | Whether matching is case-sensitive. |
| `GroupNumber` | `groupNumber` | `int` | `0` | The capture group to extract as the matched value (`0` is the whole match). |
| `Validator` | `validator` | `string` or object | `null` | An optional named, post-match validator (see below). |

### Validators

A regular expression matches a *format*, not a valid value. The optional `validator` runs a named, built-in check on each match and keeps the match only if the check passes, so a generic identifier can reject format-valid but checksum-invalid values without embedding executable code in the policy.

The validator may be written as a string, or as an object when it takes parameters:

```json
"validator": "luhn"
```

```json
"validator": { "name": "mod11", "params": { "variant": "cpf" } }
```

An unknown or not-yet-implemented validator name is a policy error and the filter raises rather than silently skipping the check. The available validators match the Phileas (Java) implementation for the same input.

| Validator | Parameters | Description |
|---|---|---|
| `luhn` | none | Standard mod-10 Luhn checksum over the digits of the match (separators ignored). |
| `mod11` | `variant`: `cpf` or `cnpj` | Weighted-sum mod-11 check digits for the Brazilian CPF and CNPJ. |
| `mod97` | `variant`: `nir` or `iban`; `substitutions` (nir) | Control from a value mod 97: the French INSEE/NIR (with Corsica substitutions) or an IBAN (MOD-97-10). |
| `mod23-letter` | `substitutions` | Control letter from a 23-entry table, for the Spanish DNI and NIE (leading X/Y/Z substitution). |
| `es-cif` | none | Spanish CIF control character (digit or letter). |
| `de-steuerid` | none | German tax ID (Steuer-ID): digit-repetition rule plus ISO/IEC 7064 MOD 11,10 check digit. |
| `de-personalausweis` | none | German ID card number: ICAO 9303 7-3-1 check digit. |
| `bic-structural` | none | SWIFT/BIC structure (ISO 9362) with a valid ISO 3166 country segment. |

---

## Ignored Values

Use the `ignored` list on an identifier to whitelist specific values:

```csharp
var policy = new Policy
{
    Name = "policy",
    Identifiers = new Identifiers
    {
        EmailAddress = new EmailAddress
        {
            Ignored = new List<string> { "no-reply@example.com" }
        }
    }
};
```

---

## Ignored Patterns

Use `ignoredPatterns` to whitelist tokens matching a regular expression:

```csharp
var policy = new Policy
{
    Name = "policy",
    Identifiers = new Identifiers
    {
        EmailAddress = new EmailAddress
        {
            IgnoredPatterns = new List<IgnoredPattern>
            {
                new IgnoredPattern
                {
                    Name = "internal-emails",
                    Pattern = @"^[\w.+-]+@internal\.corp$",
                    CaseSensitive = false
                }
            }
        }
    }
};
```

| Property | JSON key | Type | Default | Description |
|---|---|---|---|---|
| `Name` | `name` | `string?` | `null` | Human-readable name for the pattern. |
| `Pattern` | `pattern` | `string?` | `null` | Regular expression to match against the detected token. |
| `CaseSensitive` | `caseSensitive` | `bool` | `false` | Whether the pattern match is case-sensitive. |

---

## Global Ignored Values

The top-level `Ignored` and `IgnoredPatterns` lists apply **across all identifier types** — any span whose text matches is dropped, no matter which filter produced it.

Each `Ignored` entry is a named set of terms:

| Property | JSON key | Type | Default | Description |
|---|---|---|---|---|
| `Name` | `name` | `string?` | `null` | Optional name for the set. |
| `Terms` | `terms` | `List<string>` | `[]` | Exact values to ignore. |
| `Files` | `files` | `List<string>` | `[]` | Files whose lines provide additional ignored terms. |
| `CaseSensitive` | `caseSensitive` | `bool` | `false` | Whether term comparison is case-sensitive. |

```csharp
var policy = new Policy
{
    Name = "policy",
    Ignored = new List<Ignored>
    {
        new Ignored
        {
            Name = "test-values",
            Terms = new List<string> { "000-00-0000", "test@example.com" },
            CaseSensitive = false
        }
    }
};
```

---

## PostFilters

`PostFilters` (on `Config.PostFilters`) controls lightweight cleanup applied to each replaced token after the strategy produces a replacement value.

| Property | JSON key | Type | Default | Description |
|---|---|---|---|---|
| `RemoveTrailingPeriods` | `removeTrailingPeriods` | `bool` | `true` | Strip trailing period characters from the replacement. |
| `RemoveTrailingSpaces` | `removeTrailingSpaces` | `bool` | `true` | Strip trailing whitespace from the replacement. |
| `RemoveTrailingNewLines` | `removeTrailingNewLines` | `bool` | `true` | Strip trailing newline characters from the replacement. |

```csharp
var policy = new Policy
{
    Name = "my-policy",
    Config = new Config
    {
        PostFilters = new PostFilters
        {
            RemoveTrailingNewLines = true,
            RemoveTrailingPeriods  = false,
            RemoveTrailingSpaces   = true
        }
    }
};
```

---

## Serializing Policies

Use `PolicySerializer` to convert policies to and from JSON:

```csharp
using Phileas.Policy;

string json = PolicySerializer.SerializeToJson(policy);
Policy loaded = PolicySerializer.DeserializeFromJson(json);
```

`PolicySerializer` omits null fields (matching the canonical schema) and resolves `${ENV_VAR}` / `env:NAME` placeholders from environment variables during deserialization. Policies can also be authored in **PhiSQL** and compiled with `Policy.FromPhiSQL(phisql)`.

---

## Example: Full Policy

```csharp
var policy = new Policy
{
    Name = "full-example",
    Config = new Config
    {
        Splitting = new Splitting { Enabled = true, Threshold = 5000 }
    },
    Identifiers = new Identifiers
    {
        Ssn = new Ssn
        {
            Strategies = new List<SsnFilterStrategy>
            {
                new SsnFilterStrategy { Strategy = "MASK" }
            },
            Ignored = new List<string> { "000-00-0000" }
        },
        EmailAddress = new EmailAddress
        {
            Strategies = new List<EmailAddressFilterStrategy>
            {
                new EmailAddressFilterStrategy { Strategy = "HASH_SHA256_REPLACE" }
            }
        }
    }
};
```
