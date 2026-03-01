# Supported Identifiers

phileas-net ships with 21 built-in PII identifier types plus a configurable **dictionary** filter. Each type is enabled by setting the corresponding property on the `Identifiers` object inside a `Policy`.

## Quick Reference

| Property Name | JSON Key | Description |
|---|---|---|
| `Age` | `age` | Numeric age expressions (e.g. "42 years old") |
| `BankRoutingNumber` | `bankRoutingNumber` | US ABA bank routing numbers |
| `BitcoinAddress` | `bitcoinAddress` | Bitcoin wallet addresses |
| `CreditCard` | `creditCard` | Credit and debit card numbers |
| `Currency` | `currency` | Currency amounts (e.g. "$1,234.56") |
| `Date` | `date` | Calendar dates in common formats |
| `Dictionaries` | `dictionaries` | One or more named lists of custom terms to redact |
| `DriversLicense` | `driversLicense` | US driver's license numbers |
| `EmailAddress` | `emailAddress` | Email addresses |
| `IbanCode` | `ibanCode` | International Bank Account Numbers |
| `IpAddress` | `ipAddress` | IPv4 and IPv6 addresses |
| `MacAddress` | `macAddress` | Network MAC addresses |
| `PassportNumber` | `passportNumber` | Passport numbers |
| `PhoneNumber` | `phoneNumber` | US and international phone numbers |
| `PhoneNumberExtension` | `phoneNumberExtension` | Phone number extensions (e.g. "ext. 123") |
| `Ssn` | `ssn` | US Social Security Numbers |
| `StateAbbreviation` | `stateAbbreviation` | Two-letter US state codes |
| `StreetAddress` | `streetAddress` | US street addresses |
| `TrackingNumber` | `trackingNumber` | Shipping/parcel tracking numbers |
| `Url` | `url` | HTTP/HTTPS URLs |
| `Vin` | `vin` | Vehicle Identification Numbers |
| `ZipCode` | `zipCode` | US ZIP codes (5-digit and ZIP+4) |

---

## Common Configuration

Every identifier type inherits from `AbstractPolicyFilter`:

```csharp
public abstract class AbstractPolicyFilter
{
    public List<string>? Ignored { get; set; }
    public List<IgnoredPattern>? IgnoredPatterns { get; set; }
    public string Sensitivity { get; set; } = "medium";
    public int Priority { get; set; } = 0;
}
```

In addition, each identifier exposes a `Strategies` list that lets you override the default `REDACT` behaviour. See [Filter Strategies](filter-strategies.md) for all available strategies.

---

## Identifier Details

### Age

Detects age expressions such as "42 years old" or "aged 35".

```csharp
Identifiers = new Identifiers { Age = new Age() }
```

```json
"identifiers": { "age": {} }
```

---

### Bank Routing Number

Detects 9-digit ABA routing numbers.

```csharp
Identifiers = new Identifiers { BankRoutingNumber = new BankRoutingNumber() }
```

---

### Bitcoin Address

Detects legacy (P2PKH/P2SH) and SegWit Bitcoin wallet addresses.

```csharp
Identifiers = new Identifiers { BitcoinAddress = new BitcoinAddress() }
```

---

### Credit Card

Detects credit and debit card numbers including Visa, Mastercard, Amex, Discover, and others.

```csharp
Identifiers = new Identifiers { CreditCard = new CreditCard() }
```

---

### Currency

Detects currency amounts with a symbol or ISO code prefix (e.g. `$1,234.56`, `€99.00`).

```csharp
Identifiers = new Identifiers { Currency = new Currency() }
```

---

### Date

Detects dates in common written forms (e.g. `January 1, 2024`, `01/01/2024`, `2024-01-01`).

```csharp
Identifiers = new Identifiers { Date = new Date() }
```

---

### Dictionary

Detects user-supplied terms in the input text. A policy can contain any number of dictionaries, each with its own `name` and list of `terms`. Matching is case-insensitive and whole-word.

```csharp
Identifiers = new Identifiers
{
    Dictionaries = new List<Dictionary>
    {
        new Dictionary
        {
            Name = "medical-conditions",
            Terms = new List<string> { "diabetes", "hypertension", "asthma" }
        }
    }
}
```

Multiple dictionaries can be combined in a single policy:

```csharp
Identifiers = new Identifiers
{
    Dictionaries = new List<Dictionary>
    {
        new Dictionary
        {
            Name = "conditions",
            Terms = new List<string> { "diabetes", "hypertension" }
        },
        new Dictionary
        {
            Name = "medications",
            Terms = new List<string> { "metformin", "lisinopril" }
        }
    }
}
```

```json
"identifiers": {
  "dictionaries": [
    {
      "name": "conditions",
      "terms": ["diabetes", "hypertension"]
    },
    {
      "name": "medications",
      "terms": ["metformin", "lisinopril"]
    }
  ]
}
```

#### Fuzzy Matching

The dictionary filter supports fuzzy matching to detect misspelled or near-match terms using Levenshtein distance. Enable fuzzy matching by setting `fuzzy: true` and optionally specifying a `level`:

```csharp
new Dictionary
{
    Name = "medical-conditions",
    Terms = new List<string> { "diabetes", "hypertension" },
    Fuzzy = true,
    Level = "medium"  // "low", "medium", or "high"
}
```

```json
{
  "name": "medical-conditions",
  "terms": ["diabetes", "hypertension"],
  "fuzzy": true,
  "level": "medium"
}
```

**Fuzzy matching levels:**

| Level | Max Edit Distance | Confidence |
|---|---|---|
| `low` (default) | 1 | 0.9 |
| `medium` | 2 | 0.75 |
| `high` | 3 | 0.6 |

For example, with `level: "medium"`, the term `"diabetes"` would match misspellings like `"diabetis"` (1 edit) or `"diabtes"` (2 edits), but not `"diabtees"` (3 edits).

#### Configuration Options

Each `Dictionary` entry supports the common `AbstractPolicyFilter` options (`ignored`, `ignoredPatterns`, `priority`) and an optional `dictionaryFilterStrategies` list to override the default `REDACT` behaviour, plus:

| Property | Type | Default | Description |
|---|---|---|---|
| `fuzzy` | `bool` | `false` | Enable fuzzy matching for near-match detection |
| `level` | `string` | `"low"` | Fuzzy matching sensitivity: `"low"`, `"medium"`, or `"high"` |

---

### Driver's License

Detects US state driver's license number formats.

```csharp
Identifiers = new Identifiers { DriversLicense = new DriversLicense() }
```

---

### Email Address

Detects RFC-compliant email addresses.

```csharp
Identifiers = new Identifiers { EmailAddress = new EmailAddress() }
```

```csharp
// Whitelist a specific address
Identifiers = new Identifiers
{
    EmailAddress = new EmailAddress
    {
        Ignored = new List<string> { "no-reply@example.com" }
    }
}
```

---

### IBAN Code

Detects International Bank Account Numbers in standard format (e.g. `GB29 NWBK 6016 1331 9268 19`).

```csharp
Identifiers = new Identifiers { IbanCode = new IbanCode() }
```

---

### IP Address

Detects IPv4 addresses (e.g. `192.168.1.1`) and IPv6 addresses.

```csharp
Identifiers = new Identifiers { IpAddress = new IpAddress() }
```

---

### MAC Address

Detects network hardware MAC addresses in `XX:XX:XX:XX:XX:XX` or `XX-XX-XX-XX-XX-XX` format.

```csharp
Identifiers = new Identifiers { MacAddress = new MacAddress() }
```

---

### Passport Number

Detects US passport numbers.

```csharp
Identifiers = new Identifiers { PassportNumber = new PassportNumber() }
```

---

### Phone Number

Detects US and international phone numbers in a variety of formats.

```csharp
Identifiers = new Identifiers { PhoneNumber = new PhoneNumber() }
```

---

### Phone Number Extension

Detects phone number extensions (e.g. `ext. 1234`, `x1234`).

```csharp
Identifiers = new Identifiers { PhoneNumberExtension = new PhoneNumberExtension() }
```

---

### SSN

Detects US Social Security Numbers in `NNN-NN-NNNN` format. The regex excludes invalid ranges (`000`, `666`, `900–999` area codes; `00` group; `0000` serial).

```csharp
Identifiers = new Identifiers { Ssn = new Ssn() }
```

The JSON key for the filter strategies list is `ssnFilterStrategies`:

```json
"ssn": {
  "ssnFilterStrategies": [
    { "strategy": "MASK" }
  ]
}
```

---

### State Abbreviation

Detects two-letter US state abbreviations (e.g. `CA`, `NY`, `TX`).

```csharp
Identifiers = new Identifiers { StateAbbreviation = new StateAbbreviation() }
```

---

### Street Address

Detects US street addresses (e.g. `123 Main St`, `456 Oak Ave Apt 7`).

```csharp
Identifiers = new Identifiers { StreetAddress = new StreetAddress() }
```

---

### Tracking Number

Detects parcel tracking numbers from major carriers (UPS, FedEx, USPS, DHL).

```csharp
Identifiers = new Identifiers { TrackingNumber = new TrackingNumber() }
```

---

### URL

Detects HTTP and HTTPS URLs.

```csharp
Identifiers = new Identifiers { Url = new Url() }
```

---

### VIN

Detects 17-character Vehicle Identification Numbers.

```csharp
Identifiers = new Identifiers { Vin = new Vin() }
```

---

### ZIP Code

Detects 5-digit US ZIP codes and ZIP+4 codes (e.g. `12345`, `12345-6789`).

```csharp
Identifiers = new Identifiers { ZipCode = new ZipCode() }
```

---

## Enabling Multiple Identifiers

Any combination of identifiers can be enabled in a single policy:

```csharp
var policy = new Policy
{
    Name = "comprehensive",
    Identifiers = new Identifiers
    {
        Ssn          = new Ssn(),
        CreditCard   = new CreditCard(),
        EmailAddress = new EmailAddress(),
        PhoneNumber  = new PhoneNumber(),
        IpAddress    = new IpAddress(),
        Url          = new Url(),
        Date         = new Date()
    }
};
```

When spans from different identifier types overlap, the span with the **higher confidence score** wins. Ties can be broken by setting a `Priority` on the identifier.
