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
| `Method` | `method` | `"newline"` | Split method: `"newline"`, `"width"`, or `"characters"` (`"character"` is accepted as an alias). Names are case-insensitive. An unrecognised name is a policy error. |
| `Overlap` | `overlap` | `0` | Characters each piece shares with the end of the previous piece. |

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

An unrecognised split method raises rather than falling back to another method, so a policy cannot
quietly be split by a method it did not ask for. The name is resolved whenever splitting is enabled,
not only once a document exceeds the threshold, so a mistake surfaces on the first document rather
than on the first large one.

Splitting is an internal optimisation and is not observable in the result. Each piece is located in
the original input, so span offsets index into the input you passed, and the replacements are applied
to that input, so its whitespace is preserved exactly. A document filtered with splitting enabled
produces the same output as the same document filtered without it.

Set `overlap` when an entity could straddle a piece boundary. Each piece after the first begins that
many characters earlier, so the entity is seen whole by the later piece; a span the overlap causes
both pieces to find is de-duplicated.

In one case the pieces cannot be located: a split method whose pieces are not verbatim substrings of
the input, which none of the built-in methods produce. Filtering then falls back to processing each
piece separately and concatenating the results, and on that path span offsets index the output rather
than the input and the input's whitespace is not preserved.

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
| `Enabled` | `enabled` | `bool` | `true` | Whether the filter is active. When `false` the filter is not built, so it detects nothing. Each entry of a list-valued identifier carries its own setting. |
| `Id` | `id` | `string?` | `null` | Optional label for this filter, so it can be named in logs and diagnostics. Carries no PII and has no effect on detection or redaction. |
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

An unknown or not-yet-implemented validator name is a policy error and the filter raises rather than silently skipping the check.

`verhoeff` and `damm` both treat the final digit as the check digit and ignore separators. They catch every single-digit error and every transposition of adjacent digits, which is what distinguishes them from a plain modulus check.

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
| `aba` | none | ABA routing transit number: 3-7-1 weighted sum mod 10 over exactly nine digits. |
| `verhoeff` | none | Verhoeff check digit (dihedral group D5), the last digit of the value. |
| `damm` | none | Damm check digit (quasigroup scheme), the last digit of the value. |

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
                    Pattern = @"^[\w.+-]+@internal\.corp$"
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

Matching is case-sensitive, as the Java filter's `Pattern.compile` is. Start the pattern with `(?i)`
for a case-insensitive match. An earlier `caseSensitive` field on this object has been removed: the
redaction policy schema declares only `name` and `pattern` here, so a policy carrying it did not
validate. **This changes behavior:** patterns previously matched case-insensitively by default.

---

## Global Ignored Values

The top-level `Ignored` and `IgnoredPatterns` lists apply **across all identifier types**: any span
whose text matches is dropped, no matter which filter produced it. An `IgnoredPatterns` entry that
cannot be evaluated within the regex match budget keeps the span rather than dropping it, so a
pattern that fails never leaves a detected value in the document; the pattern is reported on
`TextFilterResult.RegexTimeouts`.

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

### Schema validation

`DeserializeFromJson` validates the policy against the bundled redaction policy schema before binding
it, and throws `PolicyValidationException` listing what failed and where:

```
The policy does not match the redaction policy schema (1.3.0):
/identifiers/socialSecurity: All values fail against the false schema
```

This matters because `System.Text.Json` skips a key it does not recognise. Without validation a
misspelled filter loaded as an absent one: the policy was accepted and then quietly did not redact
what it named.

Validation is against what the policy means to this port rather than its literal text, so the
spellings documented as accepted are not errors: a strategy name in any casing, the older
`SHIFT_DATE` name, and the deprecated `identifiers.dictionary` key.

Pass `validate: false` to load a policy written for a different schema version:

```csharp
Policy loaded = PolicySerializer.DeserializeFromJson(json, validate: false);
```

Opting out means anything the schema would have rejected is skipped rather than applied, which is the
behavior this validation exists to stop. `PolicySchema.Validate(json)` and
`PolicySchema.GetValidationErrors(json)` apply the schema as written, with no leniency.

Over the REST service, `PUT /policies/{name}` returns `400` with the same detail rather than storing
a policy that would not do what it says. Reading a policy back does not re-validate it: the upload is
the gate, and a filter request would otherwise pay for validation every time.

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
