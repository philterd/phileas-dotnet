# Supported Identifiers

phileas-dotnet ships with a comprehensive set of built-in PII identifier types — pattern-based detectors, dictionary-backed name/location detectors, configurable custom dictionaries, custom regex identifiers, section detectors, and an AI-powered **PhEye** filter. Each type is enabled by setting the corresponding property on the `Identifiers` object inside a `Policy`.

## Quick Reference

### Pattern-based identifiers

| Property Name | JSON Key | Description |
|---|---|---|
| `Age` | `age` | Numeric age expressions (e.g. "42 years old") |
| `BankRoutingNumber` | `bankRoutingNumber` | US ABA bank routing numbers |
| `BitcoinAddress` | `bitcoinAddress` | Bitcoin wallet addresses |
| `CanadaSin` | `canadaSin` | Canadian Social Insurance Numbers (`NNN-NNN-NNN`, Luhn-validated) |
| `CreditCard` | `creditCard` | Credit and debit card numbers |
| `Currency` | `currency` | Currency amounts (e.g. "$1,234.56") |
| `Date` | `date` | Calendar dates in common formats |
| `DriversLicense` | `driversLicense` | US driver's license numbers |
| `Ein` | `ein` | US Employer Identification Numbers and TINs (`NN-NNNNNNN`) |
| `EmailAddress` | `emailAddress` | Email addresses |
| `IbanCode` | `ibanCode` | International Bank Account Numbers |
| `IpAddress` | `ipAddress` | IPv4 and IPv6 addresses |
| `Itin` | `itin` | US Individual Taxpayer Identification Numbers (`9XX-XX-XXXX`) |
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
| `CustomDictionaries` | `dictionaries` | Custom term lists with `classification` and `sensitivity`-based fuzzy matching |
| `CustomIdentifiers` | `identifiers` | Custom regex identifiers |
| `Sections` | `sections` | Spans of text delimited by a start and end pattern |
| `PhEyes` | `pheyes` | AI-powered NER via a remote PhEye service |

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
| `Enabled` | `enabled` | `true` | Whether the filter is active. Set it to `false` and the filter is not built, so it detects nothing and costs nothing. Each entry of a list-valued identifier (`dictionaries`, `identifiers`, `sections`, `pheyes`) carries its own setting. |
| `Id` | `id` | none | Optional label for this filter, so it can be named in logs and diagnostics. Carries no PII and has no effect on detection or redaction. |
| `Ignored` | `ignored` | `null` | Exact values that should not be redacted. |
| `IgnoredFiles` | `ignoredFiles` | `null` | Files whose lines provide additional ignored terms. |
| `IgnoredPatterns` | `ignoredPatterns` | `null` | Regex patterns whose matches are not redacted. |
| `WindowSize` | `windowSize` | `0` | Context words on each side of a match; `0` uses the default (5). |
| `Priority` | `priority` | `0` | Higher-priority filter spans win when spans overlap. |

In addition, each identifier exposes a `Strategies` list that lets you override the default `REDACT` behaviour. See [Filter Strategies](filter-strategies.md) for all available strategies.

---

## Identifier Details

### Age

Detects age expressions in the forms below.

| Written form | Example |
|---|---|
| A number with its unit | `42 years old`, `42-year-old`, `30 yo`, `3.5 yrs`, `22 y/o` |
| An `age` or `aged` keyword | `age 25`, `aged 65`, `Age: 47`, `Age = 47`, `Age - 47`, `Age:47` |

A keyword may be separated from its value by `:`, `=` or `-`, with or without surrounding
whitespace, or by whitespace alone, and that whitespace may include a line break, so a label on one
line with its value on the next is detected. A word merely ending in "age", such as `coverage: 47`,
is not a keyword.

A number following a keyword is read as an age only when it is between 0 and 125, so `Bronze Age
1200` and `form AGE 2024` are not detected. A zero-padded value, as a fixed-width record writes it
(`AGE 047`), still reads. The bound applies only to the keyword forms: a number
carrying its own unit, as in `1200 years old`, is what makes that form an age and is not
plausibility-checked.

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

### Canada SIN

Detects Canadian Social Insurance Numbers: nine digits, unformatted (`046454286`) or in three groups of three (`046 454 286`, `046-454-286`). Spans are reported as `canada-sin`.

```csharp
Identifiers = new Identifiers { CanadaSin = new CanadaSin() }
```

Every match must pass the mod-10 Luhn check, so `123 456 789` is not detected. The check digit is part of the number's definition, and the check cannot be turned off. SINs beginning with 9, which are issued to temporary residents, are detected like any other.

#### Separators and boundaries

The separators, boundaries, and intentional exclusions are the [SSN](#ssn) identifier's, which lists them in full. The SSN's range exclusions do not apply: a SIN may begin with any digit, and only the Luhn check rejects a value. Between groups a SIN accepts nothing, one hyphen or hyphen substitute (optionally followed by horizontal whitespace), one horizontal whitespace character, or a hyphen followed by a line break, so a SIN wrapped across two lines is detected. A match may not begin or end inside a run of ASCII letters, digits, or underscores, and non-ASCII digits are not accepted.

#### Leading digits and business numbers

By default a Luhn-valid value with any leading digit is detected. Set `onlyValidPrefixes` to `true` to drop values beginning with 0 or 8: neither is issued as a personal SIN, and 8 is used for Canada Revenue Agency business numbers. This rule rests on secondary sources rather than an official Government of Canada statement, so it is opt-in.

The nine-digit root of a business number also passes the Luhn check, so in the default mode business numbers are detected as SINs. `onlyValidPrefixes` removes those beginning with 8.

#### Overlap with the SSN identifier

A separated SIN (`NNN-NNN-NNN`) never matches the SSN identifier, whose groups are `NNN-NN-NNNN`. An unformatted nine-digit run can match both. When both filters are enabled, the two spans are resolved by the existing [span disambiguation](span-disambiguation.md) and overlap rules, with no SIN-specific rule: the spans have the same length and confidence, so without disambiguation the filter with the higher `priority` wins, and with equal priorities the run is reported as `ssn`. Give `canadaSin` a higher `priority` to report such runs as `canada-sin`.

The JSON key for the filter strategies list is `canadaSinFilterStrategies`:

```json
"canadaSin": {
  "onlyValidPrefixes": true,
  "canadaSinFilterStrategies": [
    { "strategy": "MASK" }
  ]
}
```

`RANDOM_REPLACE` produces a Luhn-valid SIN written in the detected value's format.

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

Detects dates in common written and numeric forms.

| Written form | Example |
|---|---|
| Month name first | `January 15, 1990`, `January 15 1990`, `Jan 15, 1990`, `Jan. 15, 1990` |
| Day first, month name | `15 January 1990`, `15 Jan 1990`, `15-Jan-1990`, `15/Jan/1990`, `15-January-1990` |
| Month first, numeric | `01/15/1990`, `01-15-1990`, `01.15.1990`, and the same with a two-digit year |
| Day first, numeric | `15/01/1990`, `15-01-1990`, `15.01.1990`, and the same with a two-digit year |
| Year first, numeric | `1990-01-15`, `1990/01/15`, `1990.01.15` |
| ISO 8601 timestamp | `2024-06-01T09:30:00Z` (the date part is the span; the time is left alone) |

A day and a month name may be separated by a space, `-`, `/` or `.`, and both separators in the date
must be the same character. A year-first date takes a zero-padded month and day, as ISO 8601 requires,
so a version string such as `2020.1.5` is not read as a date. A date run together with a word, such as
`1990-01-15x`, is not detected either; the only text allowed against the end of a date is the time of
an ISO 8601 timestamp.

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

> The date strategies list uses the JSON key `dateFilterStrategies`. The `SHIFT`, `TRUNCATE_TO_YEAR`
> and `RELATIVE` strategies (see [Filter Strategies](filter-strategies.md#shift)) are specific to the
> Date filter. Each acts on every form above, using the format the matching pattern recorded, and
> falls back to `REDACT` for a token that is not a real calendar date.

---

### Dictionary

Detects user-supplied terms in the input text. A policy can contain any number of dictionaries, each
with an optional `classification` and a list of `terms`. Matching is case-insensitive and whole-word,
and every occurrence of a term is detected, including repeats.

```csharp
Identifiers = new Identifiers
{
    CustomDictionaries = new List<CustomDictionary>
    {
        new CustomDictionary
        {
            Classification = "medical-conditions",
            Terms = new List<string> { "diabetes", "hypertension", "asthma" }
        }
    }
}
```

```json
"identifiers": {
  "dictionaries": [
    { "classification": "conditions", "terms": ["diabetes", "hypertension"] },
    { "classification": "medications", "terms": ["metformin", "lisinopril"] }
  ]
}
```

| Property | JSON key | Default | Description |
|---|---|---|---|
| `Classification` | `classification` | none | Label reported on each span this dictionary produces |
| `Terms` | `terms` | none | The terms to detect |
| `Files` | `files` | none | Files of additional terms, one per line |
| `Fuzzy` | `fuzzy` | `false` | Enable near-match detection |
| `Sensitivity` | `sensitivity` | `"off"` | How near a match may be: `"off"`, `"high"`, `"medium"`, `"low"`, `"auto"` |
| `Capitalized` | `capitalized` | `false` | Require a match to start with a capital letter |
| `Strategies` | `customFilterStrategies` | none | Filter strategies for this dictionary |

#### Fuzzy matching

With `fuzzy: true`, a term is also detected when the text is within a Levenshtein distance of it.
`sensitivity` sets how far: **higher sensitivity means a stricter match**.

| `sensitivity` | Edit distance accepted |
|---|---|
| `"off"` or `"high"` | 0 (exact only) |
| `"medium"` | 1 |
| `"low"` | 2 |

A near match is detected whether or not the same term also appears exactly elsewhere in the document,
so a name written correctly once and misspelled once has both redacted. Each occurrence is reported
once, and a span's text is the document's, not the dictionary's spelling of it, which is what a
filter strategy operates on.

```csharp
new CustomDictionary
{
    Classification = "medical-conditions",
    Terms = new List<string> { "diabetes", "hypertension" },
    Fuzzy = true,
    Sensitivity = "medium"
}
```

#### The deprecated `dictionary` key

This port also accepted `identifiers.dictionary`, a .NET-only spelling with `name` instead of
`classification` and `level` instead of `sensitivity`. The redaction policy schema declares only
`dictionaries` and rejects keys it does not define, so a policy using it did not validate.

It is still read, and its entries are folded into `dictionaries`, so an existing policy keeps
redacting. It is never written back: a policy that goes in with `dictionary` comes out with
`dictionaries`. The key is accepted in JSON only: there is no public `Identifiers` property for it, so
code sets `CustomDictionaries`. Two things change when it is folded:

- `level` counted upward (`"low"` accepted 1 edit, `"medium"` 2, `"high"` 3) while `sensitivity`
  counts downward, so the mapping goes by the distance each accepts: `level: "low"` becomes
  `sensitivity: "medium"`, and `level: "medium"` becomes `sensitivity: "low"`. Nothing accepts 3
  edits, so `level: "high"` also becomes `sensitivity: "low"` and stops matching at a distance of 3.
- Spans are reported as `custom-dictionary` rather than `dictionary`, so the default redaction label
  becomes `{{{REDACTED-custom-dictionary}}}`.

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
`v1.2.3.4`, is still detected and still redacted, as is one delimited by punctuation, such as
`build-10.0.0.1-rc`. Every IPv4 and IPv6 address reports a confidence of 0.9, as in the Java port.

A bare `::` on its own is not treated as an address. It is the unspecified address, but accepting it
meant every `::` in prose or code became a span, so `std::vector` and `Foo::Bar` were reported as IP
addresses. A match also cannot begin or end partway through a token, which keeps an address from
being reported as a fragment of one.

```csharp
Identifiers = new Identifiers { IpAddress = new IpAddress() }
```

---

### ITIN

Detects US Individual Taxpayer Identification Numbers: SSN-shaped values that always begin with 9 (`9XX-XX-XXXX`). The same three forms the [SSN](#ssn) identifier accepts are matched: hyphenated (`912-70-1234`), separated by a single whitespace character (`912 70 1234`), and unformatted (`912701234`). Spans are reported as `itin`.

```csharp
Identifiers = new Identifiers { Itin = new Itin() }
```

The separators, line-wrap handling, boundaries, and intentional exclusions are the SSN identifier's, which lists them in full. The SSN's range exclusions do not apply: a `00` group or `0000` serial is detected.

#### IRS ranges and ATINs

By default any `9XX-XX-XXXX` value is detected. Set `onlyValidRanges` to `true` to keep only values whose fourth and fifth digits fall in the ranges the IRS issues for ITINs: 50 to 65, 70 to 88, 90 to 92, and 94 to 99 ([IRS Publication 4757](https://www.irs.gov/pub/irs-pdf/p4757.pdf); [IRM 3.21.263](https://www.irs.gov/irm/part3/irm_03-021-263r), which reserves 89 and 93 for other programs). It is off by default so that a range the IRS issues after this release is still detected.

Adoption Taxpayer Identification Numbers (ATINs) also begin with 9, and their fourth and fifth digits are always 93 ([IRM 3.13.40](https://www.irs.gov/irm/part3/irm_03-013-040)). In the default mode an ATIN is reported as `itin`; with `onlyValidRanges` it is dropped.

#### Overlap with other identifiers

The SSN identifier never reports a value beginning with 9, so a value is reported as `itin` or `ssn`, never both. An unformatted value is a bare nine-digit run, which other enabled filters may also match. The existing overlap rules and [span disambiguation](span-disambiguation.md) decide between them. For example, `bankRoutingNumber` matches any nine-digit run at a higher confidence, so with both enabled, and without span disambiguation, an unformatted ITIN is reported as a bank routing number. An unformatted SSN behaves the same way.

The JSON key for the filter strategies list is `itinFilterStrategies`:

```json
"itin": {
  "onlyValidRanges": true,
  "itinFilterStrategies": [
    { "strategy": "LAST_4" }
  ]
}
```

`RANDOM_REPLACE` produces an ITIN in an IRS-issued range, written in the detected value's format.

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

> The redaction policy schema also declares `identifiers.person`, a deprecated alias carrying a single
> PhEye configuration rather than a list. A policy using it still loads: the entry is folded into
> `pheyes`, after any declared there. It is written back as `pheyes`, so a policy that goes in with
> `person` comes out with the canonical key. The key is accepted in JSON only: there is no public
> `Identifiers` property for it, so code sets `PhEyes`.

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

Detects US Social Security Numbers in `NNN-NN-NNNN` format. The regex excludes invalid ranges (`000`, `666`, and `900` to `999` area codes; `00` group; `0000` serial). A value beginning with 9 is never reported as an SSN; it is the [ITIN](#itin) identifier's.

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

Detects parcel tracking numbers from UPS, FedEx, and USPS. A number is matched only as a whole: a run of letters
or digits that no format matches in full, such as a 17-digit number, is not matched rather than redacted in part.

```csharp
Identifiers = new Identifiers { TrackingNumber = new TrackingNumber() }
```


| Property | JSON key | Default | Description |
|---|---|---|---|
| `Ups` | `ups` | `true` | Detect UPS numbers: `1Z` and sixteen letters or digits, `T` and ten digits, or 26 digits. |
| `Fedex` | `fedex` | `true` | Detect FedEx numbers: 12, 15, 20, or 22 digits. |
| `Usps` | `usps` | `true` | Detect USPS numbers: 22 or 24 digits starting `92` to `95`, 16 digits starting `70`, `14`, `23`, or `03`, two letters, nine digits, and two letters (`EA123456789US`), or 26, 28, 30, or 34 digits. |
| `AllowSpaces` | `allowSpaces` | `false` | Also detect a number written in space-separated groups. |

Letters match in either case. Each span's classification is the carrier whose format matched: `ups`, `fedex`,
or `usps`. A 22-digit number starting `92` to `95` matches both a FedEx and a USPS format and is reported as
`fedex`; a 26-digit number matches both a UPS and a USPS format and is reported as `ups`, as in the Java port.
A 20-digit USPS number starting `92` to `95` written in five groups of four (`9400 1000 0000 0000 0000`) is
detected whether or not `allowSpaces` is set.

The formats match the Java port, with two deliberate differences:

- A bare 9-digit number is not detected. The Java port treats one as a UPS number, which also matches SSNs
  written without dashes, ZIP+4 codes, and other 9-digit identifiers.
- A UPS `T` number must have ten digits after the `T`. The Java port also accepts letters there, which matches
  ordinary words such as "Temperature".

With `allowSpaces` set, the USPS international format allows a space between its letters and digits, so two
two-letter words around a 9-digit number can match: `is 123456789 on` is redacted as one tracking number,
words included. The Java port behaves the same way.

The grouped USPS form matches exactly five groups of four. Without `allowSpaces`, a grouped number with more
groups after them is redacted up to the fifth group and the rest is left: `9400 1000 0000 0000 0000 0000` keeps
its last `0000`. With `allowSpaces` set, the same number is redacted whole when its length matches a format, as
this 24-digit one does. The Java port behaves the same way.

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
