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

The `condition` property is reserved for future use. At present all strategies evaluate their condition as `true`.

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
