# Filter Strategies

A **filter strategy** controls what happens to a detected PII token. Each identifier type supports a `Strategies` list; the first strategy whose `condition` evaluates to `true` is applied. If the list is empty, the default `REDACT` strategy is used.

## Available Strategies

| Strategy | Constant | Description |
|---|---|---|
| `REDACT` | `AbstractFilterStrategy.Redact` | Replace the token with a formatted redaction label |
| `RANDOM_REPLACE` | `AbstractFilterStrategy.RandomReplace` | Replace with a random GUID (consistent within a context) |
| `STATIC_REPLACE` | `AbstractFilterStrategy.StaticReplace` | Replace with a fixed string |
| `CRYPTO_REPLACE` | `AbstractFilterStrategy.CryptoReplace` | Replace with AES-encrypted ciphertext |
| `FPE_ENCRYPT_REPLACE` | `AbstractFilterStrategy.FpeEncryptReplace` | Format-preserving encryption |
| `HASH_SHA256_REPLACE` | `AbstractFilterStrategy.HashSha256Replace` | Replace with the SHA-256 hex digest |
| `LAST_4` | `AbstractFilterStrategy.Last4` | Keep only the last 4 characters |
| `MASK` | `AbstractFilterStrategy.Mask` | Overwrite characters with a mask character |
| `SAME` | `AbstractFilterStrategy.Same` | Leave the token unchanged (mark as detected but not replaced) |
| `TRUNCATE` | `AbstractFilterStrategy.Truncate` | Keep only the first character |
| `SHIFT_DATE` | `AbstractFilterStrategy.ShiftDate` | Shift a detected date by a configurable offset (date filters only) |

---

## Strategy Details

### REDACT

Replaces the token with a formatted label. The `redactionFormat` string may contain:

- `%t` — replaced with the filter type name (e.g. `ssn`, `email-address`)
- `%l` — replaced with the token's classification label (if any)

**Default format:** `{{{REDACTED-%t}}}`

```csharp
new SsnFilterStrategy
{
    Strategy = "REDACT",
    RedactionFormat = "[REMOVED-%t]"
}
```

```json
{ "strategy": "REDACT", "redactionFormat": "[REMOVED-%t]" }
```

---

### RANDOM_REPLACE

Replaces the token with a randomly generated GUID. When a [Context Service](context-service.md) is configured, the same token always receives the same GUID within a named context, preserving referential integrity.

```csharp
new SsnFilterStrategy
{
    Strategy = "RANDOM_REPLACE"
}
```

See [Context Service](context-service.md) for details on maintaining consistency across documents.

---

### STATIC_REPLACE

Replaces the token with a fixed string supplied in `staticReplacement`. Falls back to `REDACT` format if `staticReplacement` is empty.

```csharp
new EmailAddressFilterStrategy
{
    Strategy = "STATIC_REPLACE",
    StaticReplacement = "user@redacted.invalid"
}
```

```json
{ "strategy": "STATIC_REPLACE", "staticReplacement": "user@redacted.invalid" }
```

---

### CRYPTO_REPLACE

Encrypts the token using AES and replaces it with the Base64-encoded ciphertext. Requires a `Crypto` block on the `Policy` with a valid `key` and `iv`. Falls back to `REDACT` if the policy has no `Crypto` configuration or decryption fails.

```csharp
var policy = new Policy
{
    Name = "encrypted",
    Crypto = new Crypto
    {
        Key = Convert.ToBase64String(aesKey),   // 16, 24, or 32 bytes
        Iv  = Convert.ToBase64String(aesIv)     // 16 bytes
    },
    Identifiers = new Identifiers
    {
        Ssn = new Ssn
        {
            Strategies = new List<SsnFilterStrategy>
            {
                new SsnFilterStrategy { Strategy = "CRYPTO_REPLACE" }
            }
        }
    }
};
```

---

### FPE_ENCRYPT_REPLACE

Format-preserving encryption. Requires an `Fpe` block on the `Policy` with `key` and `tweak`.

```csharp
var policy = new Policy
{
    Name = "fpe-policy",
    Fpe = new Fpe { Key = "...", Tweak = "..." },
    Identifiers = new Identifiers
    {
        CreditCard = new CreditCard
        {
            Strategies = new List<CreditCardFilterStrategy>
            {
                new CreditCardFilterStrategy { Strategy = "FPE_ENCRYPT_REPLACE" }
            }
        }
    }
};
```

---

### HASH_SHA256_REPLACE

Replaces the token with its lower-case SHA-256 hex digest. Optionally appends a random salt before hashing when `salt: true` is set.

```csharp
new EmailAddressFilterStrategy
{
    Strategy = "HASH_SHA256_REPLACE",
    Salt = true     // prepend random salt before hashing
}
```

```json
{ "strategy": "HASH_SHA256_REPLACE", "salt": true }
```

---

### LAST_4

Keeps the last four characters of the token and discards the rest. If the token is shorter than four characters, the full token is returned.

```csharp
new CreditCardFilterStrategy { Strategy = "LAST_4" }
```

Output example: `1234` (from `4111-1111-1111-1234`).

---

### MASK

Replaces characters with a mask character (default `*`). Use `maskLength` to control how many characters are written:

| `maskLength` value | Behaviour |
|---|---|
| `"same"` (default) | Mask has the same length as the original token |
| Integer string, e.g. `"6"` | Mask has exactly that many characters (capped at token length) |

```csharp
new SsnFilterStrategy
{
    Strategy = "MASK",
    MaskCharacter = "#",
    MaskLength = "same"
}
```

```json
{ "strategy": "MASK", "maskCharacter": "#", "maskLength": "6" }
```

---

### SAME

Marks the token as detected but leaves the text unchanged. Useful when you want spans and metadata without altering the output.

```csharp
new PhoneNumberFilterStrategy { Strategy = "SAME" }
```

---

### TRUNCATE

Keeps only the first character of the token.

```csharp
new EmailAddressFilterStrategy { Strategy = "TRUNCATE" }
```

---

### SHIFT_DATE

> **Date filters only.** Applies to `DateFilterStrategy`; ignored by all other filter types.

Shifts a detected date forward or backward by a configurable number of days, months, and/or years while preserving the original date format. All three offsets default to `0` and can be combined freely. Negative values shift the date into the past.

**Supported date formats**

| Example | Format |
|---|---|
| `1/15/1990` | Numeric M/D/YYYY |
| `January 15, 1990` | Full month name |
| `15-Jan-1990` | Day-abbreviated-month-year |
| `Jan 15, 1990` | Abbreviated month name |

If the detected token cannot be parsed as a date, or if the `Date` filter type is not active, `SHIFT_DATE` falls back to `REDACT`.

**Properties**

| Property | JSON key | Type | Default | Description |
|---|---|---|---|---|
| `Days` | `days` | `int` | `0` | Days to add (negative to subtract) |
| `Months` | `months` | `int` | `0` | Months to add (negative to subtract) |
| `Years` | `years` | `int` | `0` | Years to add (negative to subtract) |

**C# example**

```csharp
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;

var policy = new Policy
{
    Name = "date-shift-policy",
    Identifiers = new Identifiers
    {
        Date = new Date
        {
            DateFilterStrategies = new List<DateFilterStrategy>
            {
                new DateFilterStrategy
                {
                    Strategy = AbstractFilterStrategy.ShiftDate,
                    Years  = -1,
                    Days   = 14
                }
            }
        }
    }
};
```

**JSON policy example**

```json
{
  "name": "date-shift-policy",
  "identifiers": {
    "date": {
      "dateFilterStrategies": [{
        "strategy": "SHIFT_DATE",
        "years": -1,
        "days": 14
      }]
    }
  }
}
```

**Example transformation**

| Input | Output |
|---|---|
| `DOB: January 15, 1990` | `DOB: January 29, 1989` |
| `Admitted: 3/1/2024` | `Admitted: 3/15/2023` |

---

## Salting

Any strategy can optionally append a random 16-byte Base64 salt to the token before processing by setting `salt: true`. The generated salt is included in the `Span.Salt` field of the result so it can be recorded for auditing or reproduction.

```csharp
new SsnFilterStrategy
{
    Strategy = "HASH_SHA256_REPLACE",
    Salt = true
}
```

---

## Strategy Conditions

Strategies can include a `condition` property that controls when they are applied. When multiple strategies are defined, phileas-net evaluates their conditions in order and applies the first strategy whose condition evaluates to `true`.

```csharp
new EmailAddressFilterStrategy
{
    Strategy = "MASK",
    Condition = "confidence > 0.8 and context == \"internal\""
}
```

**Supported condition fields:**
- `confidence` - Detection confidence (0.0 to 1.0)
- `context` - Context name passed to `FilterService.Filter()`
- `token` - The detected text value
- `type` - Classification type (e.g., "PER", "LOC")

**Supported operators:**
- Comparison: `==`, `!=`, `>`, `<`, `>=`, `<=`, `is`, `is not`
- String: `startswith`
- Logical: `and`

See [Filter Conditions](filter-conditions.md) for detailed examples and usage patterns.

---

## Configuring Strategies Per Identifier

Each identifier type has a corresponding strategy class (e.g. `SsnFilterStrategy`, `EmailAddressFilterStrategy`). Set the `Strategies` list on the identifier:

```csharp
var policy = new Policy
{
    Name = "multi-strategy",
    Identifiers = new Identifiers
    {
        Ssn = new Ssn
        {
            Strategies = new List<SsnFilterStrategy>
            {
                new SsnFilterStrategy { Strategy = "MASK" }
            }
        },
        EmailAddress = new EmailAddress
        {
            Strategies = new List<EmailAddressFilterStrategy>
            {
                new EmailAddressFilterStrategy { Strategy = "HASH_SHA256_REPLACE" }
            }
        },
        PhoneNumber = new PhoneNumber
        {
            Strategies = new List<PhoneNumberFilterStrategy>
            {
                new PhoneNumberFilterStrategy { Strategy = "LAST_4" }
            }
        }
    }
};
```
