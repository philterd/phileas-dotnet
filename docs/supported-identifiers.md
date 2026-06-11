# Supported Identifiers

phileas-net ships with a comprehensive set of built-in PII identifier types — pattern-based detectors, dictionary-backed name/location detectors, configurable custom dictionaries, custom regex identifiers, section detectors, and an AI-powered **PhEye** filter. Each type is enabled by setting the corresponding property on the `Identifiers` object inside a `Policy`.

## Quick Reference

### Pattern-based identifiers

| Property Name | JSON Key | Description |
|---|---|---|
| `Age` | `age` | Numeric age expressions (e.g. "42 years old") |
| `BankRoutingNumber` | `bankRoutingNumber` | US ABA bank routing numbers |
| `BitcoinAddress` | `bitcoinAddress` | Bitcoin wallet addresses |
| `CreditCard` | `creditCard` | Credit and debit card numbers |
| `Currency` | `currency` | Currency amounts (e.g. "$1,234.56") |
| `Date` | `date` | Calendar dates in common formats |
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

### Dictionary-backed name & location identifiers

| Property Name | JSON Key | Description |
|---|---|---|
| `FirstName` | `firstName` | Common first names |
| `Surname` | `surname` | Common surnames |
| `City` | `city` | City names |
| `County` | `county` | County names |
| `State` | `state` | US state names |
| `Hospital` | `hospital` | Hospital names |

### Custom & AI identifiers

| Property Name | JSON Key | Description |
|---|---|---|
| `Dictionaries` | `dictionary` | Named lists of custom terms (legacy dictionary model, `level`-based fuzzy matching) |
| `CustomDictionaries` | `dictionaries` | Custom term lists with `classification` and `sensitivity`-based fuzzy matching |
| `CustomIdentifiers` | `identifiers` | Custom regex identifiers |
| `Sections` | `sections` | Spans of text delimited by a start and end pattern |
| `PhEyes` | `pheye` | AI-powered NER via remote service or local ONNX model |

---

## Common Configuration

Every identifier type inherits from `AbstractPolicyFilter`:

```csharp
public abstract class AbstractPolicyFilter
{
    public bool Enabled { get; set; } = true;
    public List<string>? Ignored { get; set; }
    public List<string>? IgnoredFiles { get; set; }
    public List<IgnoredPattern>? IgnoredPatterns { get; set; }
    public int WindowSize { get; set; }   // 0 = use the policy/global default
    public int Priority { get; set; }
}
```

| Property | JSON key | Default | Description |
|---|---|---|---|
| `Enabled` | `enabled` | `true` | Whether the filter is active. |
| `Ignored` | `ignored` | `null` | Exact values that should not be redacted. |
| `IgnoredFiles` | `ignoredFiles` | `null` | Files whose lines provide additional ignored terms. |
| `IgnoredPatterns` | `ignoredPatterns` | `null` | Regex patterns whose matches are not redacted. |
| `WindowSize` | `windowSize` | `0` | Context words on each side of a match; `0` uses the default (5). |
| `Priority` | `priority` | `0` | Higher-priority filter spans win when spans overlap. |

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

Detects dates in common written and numeric forms (e.g. `January 1, 2024`, `01/01/2024`, `6.4.2020`).

```csharp
Identifiers = new Identifiers { Date = new Date() }
```

| Property | JSON key | Default | Description |
|---|---|---|---|
| `OnlyValidDates` | `onlyValidDates` | `false` | When `true`, numeric dates that are not real calendar dates (e.g. `02-31-2019`) are not redacted. Month-name dates are always treated as valid. |

```csharp
Identifiers = new Identifiers
{
    Date = new Date { OnlyValidDates = true }
}
```

> The date strategies list uses the JSON key `dateFilterStrategies`. The `SHIFT_DATE` strategy (see [Filter Strategies](filter-strategies.md#shift_date)) is specific to the Date filter.

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

**Fuzzy matching levels:** a *lower* level allows *more* edits (it is more permissive), and the assigned match confidence drops accordingly.

| Level | Max Edit Distance | Confidence |
|---|---|---|
| `high` | 0 (exact match) | 0.9 |
| `medium` | 1 | 0.7 |
| `low` (default) | 2 | 0.5 |

For example, with `level: "medium"`, the term `"diabetes"` would match a misspelling like `"diabetis"` (1 edit) but not `"diabtes"` (2 edits). With `level: "low"`, both would match.

#### Configuration Options

Each `Dictionary` entry supports the common `AbstractPolicyFilter` options (`ignored`, `ignoredPatterns`, `priority`) and an optional `dictionaryFilterStrategies` list to override the default `REDACT` behaviour, plus:

| Property | Type | Default | Description |
|---|---|---|---|
| `fuzzy` | `bool` | `false` | Enable fuzzy matching for near-match detection |
| `level` | `string` | `"low"` | Fuzzy matching sensitivity: `"low"`, `"medium"`, or `"high"` |

> There are two dictionary models. The `Dictionaries` list above (JSON key `dictionary`) uses the `level`-based fuzzy matching shown here. A second `CustomDictionaries` list (JSON key `dictionaries`) carries a `classification`, optional term `files`, and uses the `sensitivity` scale (`"off"`, `"low"`, `"medium"`, `"high"`, `"auto"`) instead of `level`; its strategies list key is `customFilterStrategies`.

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

### PhEye

Detects named entities using AI-powered NLP. Supports both **remote service mode** (connects to a [PhEye](https://github.com/philterd/pheye) service) and **local model mode** (uses a local ONNX BERT-based NER model).

#### Remote Service Mode

```csharp
Identifiers = new Identifiers
{
    PhEyes = new List<PhEye>
    {
        new PhEye
        {
            PhEyeConfiguration = new PhEyeConfiguration
            {
                Endpoint = "http://localhost:8080",
                BearerToken = "your-api-token",  // Optional
                Timeout = 30,
                Labels = new List<string> { "PERSON", "ORG", "LOC" }
            }
        }
    }
}
```

JSON configuration:

```json
"identifiers": {
  "pheye": [
    {
      "phEyeConfiguration": {
        "endpoint": "http://localhost:8080",
        "bearerToken": "your-api-token",
        "timeout": 30,
        "labels": ["PERSON", "ORG", "LOC"]
      }
    }
  ]
}
```

#### Local Model Mode

```csharp
Identifiers = new Identifiers
{
    PhEyes = new List<PhEye>
    {
        new PhEye
        {
            PhEyeConfiguration = new PhEyeConfiguration
            {
                ModelPath = "C:\\models\\model.onnx",
                VocabPath = "C:\\models\\vocab.txt",
                Labels = new List<string> { "PER", "ORG", "LOC", "MISC" }
            }
        }
    }
}
```

JSON configuration:

```json
"identifiers": {
  "pheye": [
    {
      "phEyeConfiguration": {
        "modelPath": "C:\\models\\model.onnx",
        "vocabPath": "C:\\models\\vocab.txt",
        "labels": ["PER", "ORG", "LOC", "MISC"]
      }
    }
  ]
}
```

**Configuration Options:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `endpoint` | `string` | `"http://localhost:8080"` | Base URL of PhEye service (remote mode) |
| `bearerToken` | `string?` | `null` | Bearer token for authentication (remote mode) |
| `timeout` | `int` | `30` | Request timeout in seconds (remote mode) |
| `modelPath` | `string?` | `null` | Path to ONNX model file (local mode) |
| `vocabPath` | `string?` | `null` | Path to BERT vocabulary file (local mode) |
| `labels` | `string[]` | `["Person"]` | Entity labels to detect |

**Mode Selection:**
- If both `modelPath` and `vocabPath` are provided → **local model mode**
- If only `endpoint` is provided → **remote service mode**
- If both are provided → prefers local model, falls back to remote on errors
- If only one of `modelPath` or `vocabPath` is set → uses remote service

**Detected Entity Types:**
- `PERSON` / `PER` → Mapped to `FilterType.Person`
- `LOCATION` / `LOC` → Mapped to `FilterType.LocationCity`
- `ORGANIZATION` / `ORG` → Mapped to `FilterType.Other`
- `MISC` → Mapped to `FilterType.Other`

For detailed documentation, see [PhEye Filter Usage](pheye-filter-usage.md).

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

| Property | JSON key | Default | Description |
|---|---|---|---|
| `RequireDelimiter` | `requireDelimiter` | `false` | When `true`, the +4 extension must be dash-separated (`12345-6789`); an undelimited 9-digit run is not treated as a ZIP+4. |
| `Validate` | `validate` | `false` | When `true`, ZIP codes not present in the bundled census data are not redacted. |

```csharp
Identifiers = new Identifiers
{
    ZipCode = new ZipCode { Validate = true, RequireDelimiter = true }
}
```

> The ZIP code strategies list uses the (singular) JSON key `zipCodeFilterStrategy`. The `population` [filter condition](filter-conditions.md#population-conditions) also uses the bundled census data.

---

### Names and Locations (dictionary-backed)

`FirstName`, `Surname`, `City`, `County`, `State`, and `Hospital` detect entries from bundled reference dictionaries. Each supports `fuzzy` matching tuned by a `sensitivity` level and a `capitalized` flag.

```csharp
Identifiers = new Identifiers
{
    FirstName = new FirstName(),
    Surname   = new Surname(),
    City      = new City { Fuzzy = true, Sensitivity = "medium" },
    State     = new State(),
    Hospital  = new Hospital()
}
```

| Property | JSON key | Default | Description |
|---|---|---|---|
| `Fuzzy` | `fuzzy` | `false` | Enable fuzzy (near-match) detection. |
| `Sensitivity` | `sensitivity` | `"medium"` | Fuzzy sensitivity: `"off"`, `"low"`, `"medium"`, `"high"`, or `"auto"`. |
| `Capitalized` | `capitalized` | `false` | Only match terms that are capitalized in the input. |

The strategies list keys are `firstNameFilterStrategies`, `surnameFilterStrategies`, `cityFilterStrategies`, `countyFilterStrategies`, `stateFilterStrategies`, and `hospitalFilterStrategies` respectively.

---

### Custom Regex Identifiers

`CustomIdentifiers` lets you define your own regex-based identifiers. Each match is classified with the configured `classification`.

```csharp
Identifiers = new Identifiers
{
    CustomIdentifiers = new List<Identifier>
    {
        new Identifier
        {
            Classification = "employee-id",
            Pattern = @"\bEMP-\d{6}\b",
            CaseSensitive = true,
            GroupNumber = 0
        }
    }
}
```

| Property | JSON key | Default | Description |
|---|---|---|---|
| `Pattern` | `pattern` | `\b[A-Z0-9_-]{6,}\b` | The regular expression to match. |
| `Classification` | `classification` | `"custom-identifier"` | Label applied to matches. |
| `GroupNumber` | `groupNumber` | `0` | Capture group used as the matched text (`0` = whole match). |
| `CaseSensitive` | `caseSensitive` | `true` | Whether the regex is case-sensitive. |

The strategies list key is `identifierFilterStrategies`. The JSON key for the list itself on `Identifiers` is `identifiers`.

---

### Sections

`Sections` redact everything between a start pattern and an end pattern (inclusive of the markers).

```csharp
Identifiers = new Identifiers
{
    Sections = new List<Section>
    {
        new Section { StartPattern = "BEGIN PRIVATE", EndPattern = "END PRIVATE" }
    }
}
```

| Property | JSON key | Default | Description |
|---|---|---|---|
| `StartPattern` | `startPattern` | `null` | Regex marking the start of the section. |
| `EndPattern` | `endPattern` | `null` | Regex marking the end of the section. |

The strategies list key is `sectionFilterStrategies`. The JSON key for the list itself on `Identifiers` is `sections`.

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

When spans from different identifier types overlap, the **longest** span wins; ties are broken by higher **confidence**, then higher **priority**, then earlier position. Set a `Priority` on an identifier to influence the outcome.
