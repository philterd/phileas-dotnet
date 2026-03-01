# Policies

A **Policy** is the primary configuration object in phileas-net. It defines which PII types to detect, how to handle each type, global settings, and values that should never be redacted.

## Policy Structure

```csharp
public class Policy
{
    public string Name { get; set; }
    public Config Config { get; set; }
    public Crypto? Crypto { get; set; }
    public Fpe? Fpe { get; set; }
    public Identifiers Identifiers { get; set; }
    public List<Ignored> Ignored { get; set; }
    public List<IgnoredPattern> IgnoredPatterns { get; set; }
    public Graphical Graphical { get; set; }
}
```

### Equivalent JSON

```json
{
  "name": "my-policy",
  "config": { "windowSize": 5 },
  "identifiers": {
    "ssn": {},
    "emailAddress": {}
  }
}
```

---

## Config

`Config` holds global tuning parameters that apply to all filters in the policy.

| Property | Type | Default | Description |
|---|---|---|---|
| `windowSize` | `int` | `5` | Number of words on each side of a detected token that are passed to filter strategies as context. |
| `splitOnPunctuation` | `bool` | `false` | Reserved for future use. |

```csharp
var policy = new Policy
{
    Name = "my-policy",
    Config = new Config { WindowSize = 3 }
};
```

---

## Crypto

`Crypto` provides the AES key and initialisation vector used by the `CRYPTO_REPLACE` filter strategy.

| Property | Type | Description |
|---|---|---|
| `key` | `string?` | Base64-encoded 16, 24, or 32-byte AES key. |
| `iv` | `string?` | Base64-encoded 16-byte AES initialisation vector. |

```csharp
var policy = new Policy
{
    Name = "encrypted-policy",
    Crypto = new Crypto
    {
        Key = Convert.ToBase64String(aesKey),
        Iv  = Convert.ToBase64String(aesIv)
    }
};
```

See [Filter Strategies — CRYPTO_REPLACE](filter-strategies.md#crypto_replace) for usage.

---

## Fpe

`Fpe` provides the key and tweak used by the `FPE_ENCRYPT_REPLACE` (Format Preserving Encryption) strategy.

| Property | Type | Description |
|---|---|---|
| `key` | `string?` | Encryption key. |
| `tweak` | `string?` | Tweak value for FPE. |

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

Each identifier class extends `AbstractPolicyFilter` and supports the following common options:

| Property | Type | Default | Description |
|---|---|---|---|
| `ignored` | `List<string>?` | `null` | Exact values that should not be redacted. |
| `ignoredPatterns` | `List<IgnoredPattern>?` | `null` | Regex patterns whose matches are not redacted. |
| `sensitivity` | `string` | `"medium"` | Sensitivity level (reserved for future use). |
| `priority` | `int` | `0` | Higher-priority filter spans win when spans overlap. |

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

| Property | Type | Default | Description |
|---|---|---|---|
| `name` | `string?` | `null` | Human-readable name for the pattern. |
| `pattern` | `string?` | `null` | Regular expression to match against the detected token. |
| `caseSensitive` | `bool` | `false` | Whether the pattern match is case-sensitive. |

---

## Global Ignored Values

The top-level `Ignored` and `IgnoredPatterns` lists apply across all identifier types. Each entry has a `value` (exact string) and a `caseSensitive` flag.

```csharp
var policy = new Policy
{
    Name = "policy",
    Ignored = new List<Ignored>
    {
        new Ignored { Value = "000-00-0000", CaseSensitive = false }
    }
};
```

---

## Example: Full Policy

```csharp
var policy = new Policy
{
    Name = "full-example",
    Config = new Config { WindowSize = 5 },
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
