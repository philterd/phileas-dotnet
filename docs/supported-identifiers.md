# Supported Identifiers

phileas-dotnet ships with a comprehensive set of built-in PII identifier types — pattern-based detectors, dictionary-backed name/location detectors, configurable custom dictionaries, custom regex identifiers, section detectors, and an AI-powered **PhEye** filter. Each type is enabled by setting the corresponding property on the `Identifiers` object inside a `Policy`.

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
| `Ein` | `ein` | US Employer Identification Numbers and TINs (`NN-NNNNNNN`) |
| `EmailAddress` | `emailAddress` | Email addresses |
| `IbanCode` | `ibanCode` | International Bank Account Numbers |
| `IpAddress` | `ipAddress` | IPv4 and IPv6 addresses |
| `MacAddress` | `macAddress` | Network MAC addresses |
| `PassportNumber` | `passportNumber` | Passport numbers |
| `PhoneNumber` | `phoneNumber` | US and international phone numbers |
| `PhoneNumberExtension` | `phoneNumberExtension` | Phone number extensions (e.g. "ext. 123") |
| `Ssn` | `ssn` | US Social Security Numbers (`NNN-NN-NNNN`) |
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
| `PhEyes` | `pheye` | AI-powered NER via a remote PhEye service |

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

Detects credit and debit card numbers including Visa, Mastercard (both the `51-55` and `2221-2720`
ranges), American Express, Diners Club, Discover, JCB, and UnionPay.

Detection happens in two stages. The pattern matches any run of 13 to 16 digits, with spaces or
hyphens allowed between them, and the issuer prefix is then checked along with the Luhn checksum as
part of `OnlyValidCreditCardNumbers`. Keeping issuer prefixes out of the pattern is deliberate: when
they were in it, a card whose range was not listed went undetected rather than merely unvalidated,
which is the worse way for a redaction filter to fail.

A number is detected whatever grouping it is written in, so an American Express printed
`NNNN NNNNNN NNNNN` is recognised as readily as a card grouped four by four. Numbers shorter than 13
or longer than 16 digits are outside the window, which excludes the 19-digit forms some issuers use.

The consequence is that `OnlyValidCreditCardNumbers` carries more weight than its name suggests. With
it enabled, which is the default, only issuer-shaped numbers passing the checksum are redacted. With
it disabled, every run of 13 to 16 digits is redacted, including order numbers and timestamps.

Setting `OnlyWordBoundaries` to `false` also costs throughput. The pattern then has to be tried at
every offset inside a run of digits rather than only where one begins, so a document containing long
unbroken digit sequences takes substantially longer to filter: 50,000 contiguous digits takes seconds
rather than milliseconds. The per-pattern regex budget does not bound this, because it limits a
single match rather than the total time spent on a document. The default is unaffected.

```csharp
Identifiers = new Identifiers { CreditCard = new CreditCard() }
```


| Property | JSON key | Default | Description |
|---|---|---|---|
| `OnlyValidCreditCardNumbers` | `onlyValidCreditCardNumbers` | `true` | Keep only numbers that carry a known issuer prefix **and** pass the Luhn checksum. Disabling it keeps every run of 13 to 16 digits. |
| `OnlyWordBoundaries` | `onlyWordBoundaries` | `true` | Require the number to sit on a word boundary. Set to `false` to find a number embedded in a longer token, at the cost of precision and throughput. |
| `IgnoreWhenInUnixTimestamp` | `ignoreWhenInUnixTimestamp` | `false` | Drop digit runs shaped like a thirteen-digit Unix timestamp in epoch milliseconds. Relevant mainly with `OnlyValidCreditCardNumbers` set to `false`, where any digit run of the right length is detected. |

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

### EIN

Detects US Employer Identification Numbers (federal tax IDs) in the `NN-NNNNNNN` format (two digits, a hyphen, seven digits). The hyphen position distinguishes an EIN from an SSN (`NNN-NN-NNNN`); a bare nine-digit run is left to the SSN filter and span disambiguation rather than claimed as an EIN.

```csharp
Identifiers = new Identifiers { Ein = new Ein() }
```

#### Relationship to the SSN identifier

`NN-NNNNNNN` is the US taxpayer identification number (TIN) format assigned to employers, and it is
detected here, not by the [SSN](#ssn) identifier. The hyphen position is the whole distinction: after
the second digit for a TIN or EIN, after the third and fifth for an SSN. A value in this format is
reported as `ein` and never as `ssn`, so a policy that enables `ssn` alone does not detect it; enable
`ein` as well.

#### Accepted separators

The hyphen is required (a bare nine-digit run is left to the SSN identifier), and the same
separators the [SSN](#ssn) identifier accepts are accepted here:

- The ASCII hyphen-minus, or any of its substitutes: the soft hyphen (U+00AD), the dashes U+2010
  through U+2015 (which include the non-breaking hyphen U+2011), the minus sign (U+2212), and the
  small and fullwidth hyphen-minus forms (U+FE58, U+FE63, U+FF0D). The hyphen may be followed by
  horizontal whitespace, so `12-  3456789` is detected.
- A hyphen followed by a line break, so an identifier wrapped across two lines is detected. Both
  `\n` and `\r\n` count as the break, and horizontal whitespace may sit on either side of it, so an
  indented continuation line works.

The input is not normalized, so span offsets index into the original text and a span covers the
complete identifier, line break included.

#### Intentional exclusions

- Whitespace on its own. Unlike an SSN, an EIN is not detected with a space in place of the
  hyphen. Whitespace following a hyphen is not limited, as above.
- Non-ASCII digits, including the fullwidth, Arabic-Indic and Devanagari forms.
- A value with a hyphen immediately before or after it, so a fragment straddling two longer
  identifiers is not claimed: `45-6789123` inside `123-45-6789123-45-6789` produces no span.

Set `onlyValidPrefixes` to `true` to keep only matches whose two-digit prefix is one the IRS currently issues, which reduces false positives on format-valid but non-issued numbers. It defaults to `false` (match any EIN-formatted value), so a prefix issued after this release is still detected; the strict list is engine-carried and only affects the opt-in mode.

The JSON key for the filter strategies list is `einFilterStrategies`:

```json
"ein": {
  "onlyValidPrefixes": true,
  "einFilterStrategies": [
    { "strategy": "REDACT" }
  ]
}
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


| Property | JSON key | Default | Description |
|---|---|---|---|
| `OnlyStrictMatches` | `onlyStrictMatches` | `true` | Use the RFC-conformant local part, which accepts the specials the RFC permits (`!#$%&'*+/=?^_`` `{\|}~`). Set to `false` for a local part of word characters, dots and dashes only. "Strict" means strictly conformant, so it matches more, not less. |
| `OnlyValidTLDs` | `onlyValidTLDs` | `false` | Keep only addresses whose top-level domain is in the bundled IANA list. The list is a point-in-time snapshot, so a newly delegated TLD is rejected until it is refreshed. |

---

### IBAN Code

Detects International Bank Account Numbers in standard format (e.g. `GB29 NWBK 6016 1331 9268 19`).

```csharp
Identifiers = new Identifiers { IbanCode = new IbanCode() }
```


| Property | JSON key | Default | Description |
|---|---|---|---|
| `OnlyValidIBANCodes` | `onlyValidIBANCodes` | `true` | Keep only codes that pass the MOD-97-10 checksum. |
| `AllowSpaces` | `allowSpaces` | `true` | Also detect a code written in the four-character groups banks print. |

---

### IP Address

Detects IPv4 addresses (e.g. `192.168.1.1`) and IPv6 addresses in every written form: expanded
(`2001:0db8:85a3:0000:0000:8a2e:0370:7334`), compressed (`2001:db8::1`, `::1`, `2001:db8::`),
IPv4-mapped (`::ffff:192.0.2.128`), and link-local with a zone identifier (`fe80::1%eth0`).

An IPv4 address with a letter or underscore immediately against it, such as the `1.2.3.4` of
`v1.2.3.4`, is still detected and still redacted, but at a lower confidence (0.70 rather than 0.95),
because it is as likely to be a version string or an identifier. Span disambiguation weighs that
confidence when another filter claims the same text. An address delimited by punctuation, such as
`build-10.0.0.1-rc`, is cleanly bounded and keeps the higher confidence.

A bare `::` on its own is not treated as an address. It is the unspecified address, but accepting it
meant every `::` in prose or code became a span, so `std::vector` and `Foo::Bar` were reported as IP
addresses. A match also cannot begin or end partway through a token, which keeps an address from
being reported as a fragment of one.

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

Detects named entities using AI-powered NLP by connecting to a remote [PhEye](https://github.com/philterd/pheye) NER service.

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
  "pheyes": [
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

**Configuration Options:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `endpoint` | `string` | `"http://localhost:8080"` | Base URL of the PhEye service |
| `bearerToken` | `string?` | `null` | Bearer token for authentication |
| `timeout` | `int` | `30` | Request timeout in seconds |
| `labels` | `string[]` | `["Person"]` | Entity labels to detect |

**Detected Entity Types:**
- `PERSON` / `PER` → Mapped to `FilterType.Person`
- `LOCATION` / `LOC` → Mapped to `FilterType.LocationCity`
- `ORGANIZATION` / `ORG` → Mapped to `FilterType.Other`
- `MISC` → Mapped to `FilterType.Other`

For detailed documentation, see [PhEye Filter Usage](pheye-filter-usage.md).

---

### Phone Number

Detects US and international phone numbers in a variety of formats, backed by Google's [libphonenumber](https://github.com/google/libphonenumber) (the `libphonenumber-csharp` port). Text is scanned with a default region of `US`, so North American Numbering Plan numbers (`(555) 123-4567`, `+1 555 123 4567`, `555.123.4567`) and any `+`-prefixed international number (`+44 20 7946 0958`, `+33 1 42 68 53 00`, `+91 98765 43210`, `+49 30 901820`) are detected regardless of region.

```csharp
Identifiers = new Identifiers { PhoneNumber = new PhoneNumber() }
```

| Property | JSON key | Default | Description |
|---|---|---|---|
| `Region` | `region` | `US` | The region(s), ISO 3166-1 alpha-2, used to interpret numbers written without an international `+` country code. Numbers with a `+` prefix are detected regardless of this value. |

To detect national-format numbers from other countries, set one region or several. Each configured region is scanned and the results are merged, with overlapping matches de-duplicated:

```csharp
Identifiers = new Identifiers
{
    PhoneNumber = new PhoneNumber { Region = new List<string> { "US", "GB", "FR" } }
}
```

In a JSON policy, `region` takes either a single string or an array of strings:

```json
{
   "identifiers": {
      "phoneNumber": {
         "region": ["US", "GB", "FR"],
         "phoneNumberFilterStrategies": [{"strategy": "REDACT"}]
      }
   }
}
```

Region codes are matched exactly, so use the uppercase ISO 3166-1 alpha-2 form (`GB`, not `gb`). An unrecognized code leaves national-format numbers undetected; only `+`-prefixed numbers are found.

> The `region` property requires redaction policy schema 1.2.0.

---

### Phone Number Extension

Detects phone number extensions (e.g. `ext. 1234`, `x1234`).

```csharp
Identifiers = new Identifiers { PhoneNumberExtension = new PhoneNumberExtension() }
```

---

### SSN

Detects US Social Security Numbers in `NNN-NN-NNNN` format. The regex excludes invalid ranges (`000`, `666`, and `900` to `999` area codes; `00` group; `0000` serial).

```csharp
Identifiers = new Identifiers { Ssn = new Ssn() }
```

#### Relationship to the EIN identifier

This identifier covers the Social Security Number forms only: `NNN-NN-NNNN`, the same digits
separated by whitespace, and the bare nine-digit run. It does not detect the `NN-NNNNNNN` taxpayer
identification number format, where the hyphen falls after the second digit. That shape belongs to
the [EIN](#ein) identifier and is reported as `ein`.

#### Accepted separators

The three groups of an identifier may be separated by any of the following:

- Nothing at all, as in `123456789`.
- One hyphen, optionally followed by horizontal whitespace. Alongside the ASCII hyphen-minus, the
  soft hyphen (U+00AD), the dashes U+2010 through U+2015 (which include the non-breaking hyphen
  U+2011), the minus sign (U+2212), and the small and fullwidth hyphen-minus forms (U+FE58, U+FE63,
  U+FF0D) are all accepted.
- One horizontal whitespace character on its own: a space, a tab, or a non-breaking space, for
  example.
- A hyphen followed by a line break, so an identifier wrapped across two lines is still detected.
  Both `\n` and `\r\n` count as the break, and horizontal whitespace may sit on either side of it,
  so an indented continuation line works.

The input is not normalized, so span offsets index into the original text and a span covers the
complete identifier, line break included.

A match may not begin or end partway through a run of ASCII letters, digits, or underscores. Other
characters, including non-ASCII letters and digits, do not block a match, so an identifier embedded
in non-Latin text is still detected.

#### Intentional exclusions

These forms produce no span, each by design:

- A line break with no hyphen before it. Accepting one would read three unrelated numbers on three
  lines as a single identifier.
- A line break inside a group of digits, such as `078-05-11` with `20` on the next line.
- Non-ASCII digits, including the fullwidth and Arabic-Indic forms.
- More than one whitespace character between two groups that no hyphen separates. Whitespace
  following a hyphen is not limited, so `078-  05-  1120` is detected.
- Whitespace before the hyphen, as in `078 - 05 - 1120`.

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


| Property | JSON key | Default | Description |
|---|---|---|---|
| `Ups` | `ups` | `true` | Detect UPS numbers (`1Z` followed by sixteen characters). |
| `Fedex` | `fedex` | `true` | Detect FedEx numbers (twelve to fifteen digits). |
| `Usps` | `usps` | `true` | Detect USPS numbers (twenty to twenty-two digits). |
| `AllowSpaces` | `allowSpaces` | `false` | Also detect a number written in space-separated groups. |

---

### URL

Detects HTTP, HTTPS and FTP URLs.

A span ends at the last character that belongs to the URL, so a trailing `/`, `&` or `#` is kept.
Punctuation that prose puts after a URL is not, so `see http://example.com/page.` redacts the URL and
leaves the sentence's period behind. This holds for punctuation outside ASCII too, including the
ideographic full stop and comma, so a URL in Japanese or Chinese text is not extended by the sentence
mark that follows it. Punctuation inside a path is untouched either way, so `/a/b.html` and `?q=1,2`
are matched whole.

```csharp
Identifiers = new Identifiers { Url = new Url() }
```


| Property | JSON key | Default | Description |
|---|---|---|---|
| `RequireHttpWwwPrefix` | `requireHttpWwwPrefix` | `true` | Require a `http://`, `https://` or `www.` prefix. Set to `false` to also detect a bare host such as `example.com/page`, which is detected at a lower confidence. |

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
