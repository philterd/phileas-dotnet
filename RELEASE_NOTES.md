# Release Notes

All notable changes to Phileas (.NET) are recorded here. Versions follow [Semantic Versioning](https://semver.org/). The current development version is `1.7.0-preview`; `1.6.0` is the latest published release.

## 1.7.0-preview

_Unreleased._

### Added

- **The `aba`, `verhoeff` and `damm` identifier validators** ([#89](https://github.com/philterd/phileas-dotnet/issues/89)). The redaction policy schema names eleven validators for the custom `identifiers` filter and this build implemented eight, so a policy naming one of these three failed to load.
- **`config.splitting.overlap`** ([#82](https://github.com/philterd/phileas-dotnet/issues/82)). Each piece after the first shares this many characters with the end of the previous one, so an entity straddling a piece boundary is seen whole by the later piece; defaults to `0`.
- **The thirteen filter options the policy model was silently dropping** ([#84](https://github.com/philterd/phileas-dotnet/issues/84)), across `creditCard`, `emailAddress`, `ibanCode`, `trackingNumber`, `url` and `phEyeConfiguration`. **Behavior change:** `creditCard.onlyValidCreditCardNumbers` defaults to `true`, so a card-shaped number failing the Luhn checksum is no longer detected.
- **Locale-aware phone number detection via the policy's `region`** ([#53](https://github.com/philterd/phileas-dotnet/issues/53)). The `phoneNumber` filter takes one ISO 3166-1 alpha-2 code or an array of them (default `US`) for reading numbers written without a `+` country code, and `+`-prefixed numbers are detected regardless.

### Changed

- **Upgraded to Philterd.PhiSql 1.3.0**, from 1.2.0. The bundled redaction policy schema moves to 1.3.0 with it, which is the version the policy model already targeted: top-level `metadata` and the plural `zipCodeFilterStrategies` are declared there and were not in 1.2.0. `GET /api/health` reports the new version.
- **A policy is validated against the redaction policy schema when it is deserialized** ([#81](https://github.com/philterd/phileas-dotnet/issues/81)). `System.Text.Json` skips a key it does not recognise, so a misspelled filter loaded as an absent one and the policy was accepted and then quietly did not redact what it named. `PolicySerializer.DeserializeFromJson` now validates first and throws `PolicyValidationException` naming each failure and its location; `PUT /policies/{name}` returns `400` with that detail instead of storing the policy. Validation is against what the policy means to this port rather than its literal text, so a strategy name in any casing, the older `SHIFT_DATE` name and the deprecated `identifiers.dictionary` key are accepted as before. **Behavior change:** a policy carrying a key the schema does not define now fails to load where it previously loaded and under-redacted; pass `validate: false` to load a policy written for a different schema version. See [Policies → Schema validation](docs/policies.md#schema-validation).
- **A policy written by this port validates against the redaction policy schema** ([#88](https://github.com/philterd/phileas-dotnet/issues/88)). `$defs.identifiers` and the strategy definitions are `additionalProperties: false`, and three keys this port emitted are not declared there, so its own output failed validation. `identifiers.dictionary`, a .NET-only spelling of `dictionaries`, is now folded into `dictionaries` on load and never written back; `ignoredPattern.caseSensitive` is removed; and the `MAP_REPLACE` fields `caseSensitive` and `fallbackStrategy`, which always serialized, are omitted unless set, since the schema's date strategy carries neither. **Behavior changes:** an ignored pattern is matched case-sensitively, as Java compiles it, where this port defaulted to case-insensitive, so a pattern relying on that needs a leading `(?i)`; and a folded `dictionary` reports its spans as `custom-dictionary`, with `level` mapped to the `sensitivity` accepting the same edit distance (`low` to `medium`, `medium` and `high` to `low`, since nothing accepts the three edits `level: high` allowed). The unreachable `DictionaryFilter` and its `FilterType.Dictionary` are removed with it. See [Supported Identifiers → Dictionary](docs/supported-identifiers.md#dictionary).
- **`GET /api/status` is removed and `GET /api/health` reports `"status": "UP"`** ([#71](https://github.com/philterd/phileas-dotnet/issues/71)), matching the health contract Philter 4.0 standardized across Philterd products (philterd/philter#90); an unhealthy service answers `"DOWN"` with `503` as before. **Breaking for anything probing the Philter-compatible surface:** a load-balancer check or client on `/api/status`, or matching the old `Healthy` value, must be updated. The response's `redactionPolicySchemaVersion` is now read from the PhiSQL reference library rather than written into the handler, where it had gone stale at `1.1.0` against an actual supported version of `1.2.0`.
- **Every regular expression now runs under a match budget, and a timeout is reported instead of being silently absorbed** ([#98](https://github.com/philterd/phileas-dotnet/issues/98)). `FilterConfiguration.RegexTimeoutMs` (default `1000`) now applies wherever a pattern runs over document text, and `TextFilterResult.RegexTimeouts` lists the patterns that gave up, so a non-empty list means part of the input went unsearched.
- **Strategy names are matched without regard to case on every filter** ([#113](https://github.com/philterd/phileas-dotnet/pull/113)). The schema's enum is uppercase, so this is leniency toward a hand-written policy rather than a second spelling to document.

### Fixed

- **A filter strategy now behaves the same on a dictionary filter as on a regex one** ([#125](https://github.com/philterd/phileas-dotnet/issues/125)). The two were built by separate code paths, and the dictionary path hand-copied eleven named properties from the policy strategy and never passed the policy's `crypto` and `fpe` blocks to the filter. Three strategies were broken as a result on `surname`, `firstName`, `city`, `county`, `state`, `hospital` and `dictionaries`: `MAP_REPLACE` never received its lookup table and silently fell through to its fallback, and `CRYPTO_REPLACE` and `FPE_ENCRYPT_REPLACE` **threw** `InvalidOperationException` reporting a missing crypto or FPE property against a policy that supplied one. The two paths now share one property copy, one `MAP_REPLACE` resolution and one configuration builder, so a property added to the policy model reaches both without a second edit. See [Filter Strategies](docs/filter-strategies.md).
- **The fuzzy dictionary filter no longer drops a near match that sits alongside an exact one** ([#126](https://github.com/philterd/phileas-dotnet/issues/126)). Its near-match scan ran only when a term had no exact match anywhere in the document, so a name written correctly once and misspelled once kept the misspelling, which is the case fuzzy matching exists for. The scan now runs either way, and an occurrence already covered exactly is not reported twice. Two further defects in the same filter are fixed with it: the ignored check was passed the whole document instead of the matched value, so it never fired and a per-filter `ignored` term never suppressed a fuzzy dictionary match, which it now does alongside #124; and a span reported the dictionary's spelling of a term rather than the document's, so its text disagreed with its own offsets. **Behavior change:** a strategy now operates on the text as written in the document, which is what `SetDictionaryFilter` has always done, so `HASH_SHA256_REPLACE` hashes `smith` rather than the dictionary's `Smith` and `LAST_4` keeps the document's casing. The two dictionary filters previously disagreed with each other on the same input; they now agree. See [Supported Identifiers → Dictionary](docs/supported-identifiers.md#dictionary).
- **An ignored pattern now suppresses a detection wherever the policy declares it** ([#124](https://github.com/philterd/phileas-dotnet/issues/124)). Two of the three placements did nothing. The top-level `ignoredPatterns` list was never applied: the sibling `ignored` terms list at the same level was, and the documentation said both applied across all identifier types. Separately, `SetDictionaryFilter` and `FuzzyDictionaryFilter` returned their spans without running their post-filters, so per-filter `ignoredPatterns`, per-filter `ignored` terms and the `config.postFilters` rules reached no dictionary-backed filter: `surname`, `firstName`, `city`, `county`, `state`, `hospital` or `dictionaries`. Both are fixed, and a top-level pattern that exceeds the regex match budget keeps the span rather than dropping it, as the per-filter path already did, so a pattern that cannot be evaluated never leaves a detected value in the document. **Behavior change:** a policy carrying an ignored pattern in either of those two placements stops redacting the values it names, which is what it asked for. Dictionary span boundaries are unchanged: those filters already trimmed punctuation themselves, so the post-filters have nothing left to trim. See [Policies → Global Ignored Values](docs/policies.md#global-ignored-values).
- **A filter switched off with `enabled: false` is no longer built and no longer redacts** ([#123](https://github.com/philterd/phileas-dotnet/issues/123)). The setting is declared by the redaction policy schema on every identifier filter and bound onto the policy model, and nothing read it, so a policy that turned a filter off still redacted with it, with no way to tell from the output that the setting had been ignored. All thirty-two filter types now honour it, as does each entry of the list-valued identifiers (`dictionaries`, `identifiers`, `sections`, `pheyes`), and a disabled filter is skipped at construction rather than built and then having its spans discarded. **Behavior change:** a policy carrying `enabled: false` stops redacting with that filter, which is what it asked for, so output that previously came back redacted may now come back intact. See [Supported Identifiers](docs/supported-identifiers.md).
- **Dictionary filters redacted the wrong text when a term also appeared inside an earlier word, and detected a repeated term only once** ([#119](https://github.com/philterd/phileas-dotnet/issues/119)). N-gram positions were resolved by searching the document for the n-gram's text, which returns the first place that text occurs rather than the words the n-gram was built from: with `Smith` in the dictionary, `Smithson Smith` redacted the first five characters of `Smithson` and left the real `Smith` in the document, and `Smith Smith Smith` was detected once. Each n-gram now carries the position of its own words, tracked while splitting, and the fuzzy filter reports every exact occurrence of a term rather than only the first. This affected `surname`, `firstName`, `city`, `county`, `state`, `hospital` and `dictionaries`, on both the exact and fuzzy paths; the regex-based `identifiers.dictionary` filter was never affected. The same defect is open upstream as philterd/phileas#393.
- **An `age` keyword may be separated from its value, so `Age: 47` is detected** ([#68](https://github.com/philterd/phileas-dotnet/issues/68)). The pattern allowed only whitespace between the keyword and the number, missing the form age most often takes in a structured clinical or intake record; `:`, `=` and `-` are now accepted with or without surrounding whitespace, and the whitespace may include a line break.
- **A number following an `age` keyword is bounded to a plausible age** ([#69](https://github.com/philterd/phileas-dotnet/issues/69)). The keyword pattern kept any number at all, so `Bronze Age 1200` and `form AGE 2024` were redacted as ages; a keyword value is now read as an age only when it is between 0 and 125, while the forms carrying their own unit, such as `1200 years old`, are unbounded as before.
- **The date strategies act on a day-first date instead of leaving it in the document** ([#115](https://github.com/philterd/phileas-dotnet/issues/115)). `SHIFT` re-parsed the token with the invariant culture, which reads a numeric date month first, so `15/01/1990` failed to parse and was returned unchanged; all three date strategies now parse with the format the matching pattern recorded, and a date that still cannot be parsed is redacted.
- **ISO 8601 and several day-first dates are detected** ([#111](https://github.com/philterd/phileas-dotnet/issues/111)). The numeric patterns all placed the year last so nothing matched `1990-01-15`, and the day-first month-name pattern took only the full month name separated by whitespace, so `15 Jan 1990`, `15-Jan-1990` and `15/Jan/1990` were missed.
- **The `TRUNCATE_TO_YEAR` and `RELATIVE` date strategies are implemented instead of silently redacting** ([#109](https://github.com/philterd/phileas-dotnet/issues/109)). Both are named by the redaction policy schema and neither existed, so a policy asking for a year or a relative phrase had the date redacted with nothing reported. **Behavior change:** a date strategy name this build does not implement now raises rather than quietly redacting.
- **The date `SHIFT` strategy reads the field names the policy schema defines** ([#80](https://github.com/philterd/phileas-dotnet/issues/80)). It bound `days`, `months` and `years` where the schema uses `shiftDays`, `shiftMonths` and `shiftYears`, and matched `SHIFT_DATE` where the schema uses `SHIFT`, so a compiled policy redacted the date instead of shifting it.
- **A `::` in prose or code is no longer reported as an IPv6 address** ([#76](https://github.com/philterd/phileas-dotnet/issues/76)). The compressed patterns matched a bare `::` with no boundary on either side, so `std::vector` and `Foo::Bar` produced ragged spans; an IPv4 address with a letter or underscore against it, such as the `1.2.3.4` of `v1.2.3.4`, is now detected at a lower confidence of 0.70.
- **A trailing `/`, `&` or `#` is no longer dropped from a URL span** ([#78](https://github.com/philterd/phileas-dotnet/issues/78)). Both patterns ended with `\b`, which cut the last character off a URL that legitimately ended in one, so a span must now end on a character a URL may end on, which keeps sentence punctuation out including outside ASCII.
- **An unrecognised split method is a policy error instead of being silently replaced with newline splitting** ([#105](https://github.com/philterd/phileas-dotnet/issues/105)). `SplitFactory` quietly substituted newline splitting for any name it did not know, so a policy was split by a method it never asked for; `character` is now accepted as an alias for `characters` and an unrecognised name raises.
- **Credit cards are detected by shape and validated by issuer, so cards in newer ranges are no longer missed** ([#102](https://github.com/philterd/phileas-dotnet/issues/102)). Issuer prefixes were encoded in the detection pattern, so Mastercard's 2221-2720 range and UnionPay went undetected rather than merely unvalidated, and recall rises from 0.800 to 1.000 with precision unchanged at 1.000. **Behavior change:** with `onlyValidCreditCardNumbers` set to `false` every run of 13 to 16 digits is now redacted, and a formatted 16-digit number with no issuer prefix no longer is.
- **Span offsets index into the input when splitting is enabled, and the input's whitespace is preserved** ([#92](https://github.com/philterd/phileas-dotnet/issues/92)). Splitting filtered each piece independently and concatenated the results, so `CharacterStart` and `CharacterEnd` described the output rather than the input; a document now filters identically with and without splitting. This is the .NET occurrence of philterd/phileas#386.
- **The EIN filter accepts the same separators as the SSN filter, and ASCII digits only** ([#95](https://github.com/philterd/phileas-dotnet/issues/95)). It now takes every hyphen substitute the SSN filter takes and an identifier wrapped across a line break, while rejecting non-ASCII digits and an adjacent hyphen.
- **SSN detection accepts Unicode hyphens and wrapped identifiers, and no longer matches digits on separate lines** ([#93](https://github.com/philterd/phileas-dotnet/issues/93)). The separator now covers the soft hyphen, the U+2010 to U+2015 dashes, the minus sign and the small and fullwidth forms, and a line break counts as part of it only when a hyphen precedes it.

## 1.6.0 - 2026-07-19

### Added

- **PDF graphical bounding boxes support open-ended page ranges.** A box's `page` may now be `0` (covers **every** page) or a negative value `-N` (covers page N through the **last** page, e.g. `-2` is "all but the first page"), in addition to an exact 1-based page number. This lets a single box span a document — a logo, watermark, or footer — without knowing the page count. See [PDF Redaction → Graphical bounding boxes](docs/pdf-redaction.md#graphical-bounding-boxes).
- **Word (`.docx`) and Excel (`.xlsx`) redaction** via the new `WordDocumentRedactor` and `XlsxRedactor` services in `Phileas.Services.Office`. They redact document text in place with the open-source Open XML SDK (no license key) and remain cross-platform (`net8.0;net10.0`, no Windows dependency). Each has file-path and `byte[]` (in-memory) overloads for `Redact`/`Detect`/`ApplySpans`, returning `OfficeRedactionSpan` records. This is .NET-port-specific container-format support (the Java and Python ports remain PDF-only for documents). See [Word & Excel Redaction](docs/office-redaction.md).
- **EIN (Employer Identification Number) detection** via the new `ein` identifier filter. It detects the canonical `NN-NNNNNNN` format at word boundaries; the hyphen position keeps it distinct from an SSN (`NNN-NN-NNNN`), and a bare nine-digit run is left to the SSN filter and span disambiguation rather than claimed as an EIN. An optional `onlyValidPrefixes` (default `false`) restricts matches to the two-digit prefixes the IRS currently issues. Requires redaction-policy schema 1.2.0 (Philterd.PhiSql 1.2.0). See [Supported Identifiers → EIN](docs/supported-identifiers.md#ein).
- **`MAP_REPLACE` filter strategy**, which replaces a detected value from a lookup table built from inline `mappings` and/or tab-separated `mappingFiles` (inline entries win; `caseSensitive` defaults to `false`). A token absent from the table can be sent to a named `generator` from the new top-level `generators` block (an `ollama` type calling a local `/api/generate` endpoint inside your boundary, with a required `timeoutMs`); a generator failure, timeout, blank output, a value equal to the original token, or a generated value that re-scans as containing PII all fall back to `fallbackStrategy` (default `REDACT`), so a detected value is never left in the clear and a generator cannot reintroduce PII. Generated values reuse the same context-scoped cache as `RANDOM_REPLACE`, so a repeated token in a `CONTEXT` scope is not regenerated. See [Filter Strategies → MAP_REPLACE](docs/filter-strategies.md#map_replace).
- **Per-strategy redaction-bar `color` for PDF and image output** ([#63](https://github.com/philterd/phileas-dotnet/issues/63)). A strategy's optional `color` colors the bar over the spans it redacts, overriding the policy-wide `config.pdf.redactionColor` (resolution order: strategy `color`, then `config.pdf.redactionColor`, then black). All three color settings (this, `redactionColor`, and `boundingBoxes[].color`) now accept a named color (`black`, `white`, `red`, `orange`, `yellow`, `green`, `blue`, `gray`) or 6-digit hex like `#ff8800`; unrecognized values render black. No effect on text redaction. See [Filter Strategies → Redaction Bar Color](docs/filter-strategies.md#redaction-bar-color).

### Fixed

- **International phone numbers are now detected** ([#55](https://github.com/philterd/phileas-dotnet/issues/55)). The phone filter now scans with Google's libphonenumber (`libphonenumber-csharp`), matching the Java filter (region `US`, `Leniency.Possible`), so `+`-prefixed international numbers the old NANP-only regex missed are redacted; NANP formats still match. See [Supported Identifiers → Phone Number](docs/supported-identifiers.md#phone-number).
- **The `ABBREVIATE` strategy now returns a value's initials** (e.g. `John Smith` produces `JS`) for `SURNAME`, `FIRST_NAME`, and the PhEye person path, instead of fully redacting.

## 1.5.0 - 2026-07-03

### Added

- **`FilterService` now accepts an injected `IContextService`** (constructor overload), so consistent RANDOM_REPLACE replacements can be backed by a durable, caller-supplied store instead of only the default in-memory one.
- **PDF redaction now detects PII in annotations and AcroForm fields.** Text in annotation contents (sticky notes, free-text, etc.) and form-field values lives outside the page content stream, so it was previously not detected. It is now extracted and run through the detector. Because this text is not rendered into the rasterized output, it is already removed from the redacted document; the detected spans carry a page number but no bounding box, so nothing is burned in. Extraction is best-effort: a malformed annotation or form never fails the redaction.

### Changed

- **Street address detection is substantially broader.** The filter now recognizes leading and trailing directionals (`123 N Main St`, `Main St NW`), ordinal street names (`123 5th Avenue`), house-number ranges and letter suffixes (`123-125`, `123A`), saint/abbreviated names (`100 St. Charles Avenue`), and a much larger set of street types. Secondary unit designators (`Apt 4B`, `Suite 200`, `Unit 12`, `#5`) are folded into the redacted span, and PO boxes (`PO Box 1234`, `P.O. Box 56`, `Post Office Box 789`) are detected.
- **Driver's license detection adds the 1-letter + 12-digit format** (Florida, Maryland, and Michigan-style, 13-character license numbers).
- **Passport detection adds the all-numeric 9-digit US passport book number.** It is attributed to the passport filter (at a low confidence, given ambiguity with SSNs and driver's-license numbers) when both filters are enabled.

### Fixed

- **The date filter now detects day-first numeric dates** (e.g. `25/12/1980`); previously only month-first numeric dates were matched. Ambiguous dates (e.g. `03/04/1981`) are detected once, and `onlyValidDates` still drops impossible calendar dates.
- **The IBAN filter now confirms the MOD-97-10 checksum.** A structurally IBAN-shaped string with wrong check digits is rejected rather than redacted.

## 1.4.0

### Changed

- **PDF text extraction is decoupled from PdfPig** so an `ITextExtractor` can supply positioned lines
  from any source (a text layer, OCR of scanned pages, etc.); text-layer redaction is unchanged.
  **Breaking:** `PdfLine.LettersByChar` (PdfPig `Letter?`) is replaced by `PdfLine.CharBoxes` (the new
  library-independent `CharBox`), and the `PdfLine` constructor changed to match.

## 1.3.0

### Changed

- **PDF detection now runs once per page instead of once per line.** `PdfFilterService` previously ran the
  full detection pipeline on every extracted text line, so a page incurred one detector pass per line. It now
  concatenates a page's lines into a single detection pass and maps each detected span back to its line for the
  bounding box. This is a large speedup for the on-device name model (GLiNER), whose fixed per-call cost
  dominated — e.g. redacting a dense 2-page PDF with name detection enabled dropped from ~72&#160;s to ~3&#160;s
  (about 25×). The model still chunks each page's text internally to stay within its `max_len`, so dense pages
  are handled without exceeding the model context.

  Because detection now sees the whole page, an entity that wraps across a line break (e.g. a name split
  over two lines) is detected and redacted — it is split into one redaction box per line it covers. This is
  an improvement over the previous per-line behavior, which could not detect a wrapped entity at all.
  Structured (regex) detection is unaffected: no built-in filter uses line anchors or multiline mode.

## 1.2.0

Built on PhiSQL 1.1.0.

### Added

- **Validator field on the custom `identifier` filter.** A custom identifier may now declare an optional `validator`,
  a named, built-in post-match check; a regex match is kept only if the validator passes, so a generic identifier can
  reject format-valid but checksum-invalid values without embedding any executable code in the policy. The validator may
  be written as a string (`"validator": "luhn"`) or as an object with a `name` and optional `params`. An unknown or
  not-yet-implemented validator name is a policy error rather than being silently ignored. This is the parity port of the
  Phileas (Java) validator field and requires redaction policy schema 1.1.0.
- **`luhn` validator** (standard mod-10 Luhn checksum), a parity port of the Phileas (Java) implementation.
- **`mod11` validator** (weighted-sum mod-11 check digits) with `cpf` and `cnpj` variants for the Brazilian CPF and CNPJ.
- **`mod97` validator** (control derived from the value mod 97) with `iban` and `nir` variants (the French INSEE/NIR
  includes Corsica substitutions).
- **`mod23-letter` validator** (control letter from a 23-entry table) for the Spanish DNI and NIE.
- **`es-cif` validator** for the Spanish CIF (organization tax ID).
- **`de-steuerid` validator** for the German tax ID (Steuer-ID), using the digit-repetition rule and the ISO/IEC 7064
  MOD 11,10 check digit.
- **`de-personalausweis` validator** for the German ID card number (ICAO 9303 7-3-1 check digit).
- **`bic-structural` validator** for SWIFT/BIC codes (ISO 9362 structure with a valid ISO 3166 country segment).
- **Token-aware chunking for local GLiNER inference.** `GlinerModel` now reads the model's `max_len` from
  `gliner_config.json` (default `384`) and splits long inputs into chunks that each stay within the token limit, so
  text longer than the model length is no longer silently truncated. Chunks overlap by `max_width - 1` words so an
  entity on a chunk boundary is still detected, and detections are returned at absolute character offsets. If text
  genuinely cannot be made to fit (the label prompt leaves no room, or a single unbroken word exceeds one chunk), the
  filter throws rather than dropping tokens silently. See [docs/pheye-filter-usage.md](docs/pheye-filter-usage.md).
- **Multi-targets .NET 8 and .NET 10.** The package targets `net8.0` and `net10.0`, so it can be consumed from .NET 8 (LTS) as well as .NET 10.
- **Symbols package and SourceLink.** The package ships a symbols package (`.snupkg`) with SourceLink, so consumers can step into Phileas source while debugging.

### Changed

- The `Philterd.PhiSql` dependency is now 1.1.1 (up from 1.1.0). It defines the `validator` field in the redaction policy schema and adds a `net8.0` build, which is what lets this package target .NET 8.

### Fixed

- **PhEye identifier block now reads the canonical `identifiers.pheyes` key.** The policy model bound the singular
  `pheye`, so a schema-conformant PhEye policy — including a PhiSQL-compiled local GLiNER `MODEL` policy, which emits
  `identifiers.pheyes` — was silently ignored and no detection ran. It now binds `pheyes`, matching the Phileas policy
  schema, the Java reference, and PhiSQL output, so local on-device (ONNX) and remote PhEye policies take effect.

## 1.1.0

Built on PhiSQL 1.1.0 and adds local, on-device entity detection.

### Added

- **Local GLiNER inference for the PhEye filter.** Set `PhEyeConfiguration.ModelPath` to a GLiNER model directory and
  the filter detects entities entirely in-process with the ONNX Runtime, with no network call and no PhEye service.
  The model directory holds the exported ONNX graph (`model.onnx` or `model_quantized.onnx`), the SentencePiece
  tokenizer (`spm.model`), and `gliner_config.json`. GLiNER is zero-shot, so `Labels` is the detection prompt, and the
  new `PhEyeConfiguration.Threshold` (default `0.5`) sets the minimum span confidence. Local detections flow through
  the same threshold, ignore, replacement, referential-integrity, and overlap pipeline as remote ones.
- New package dependencies for local inference: `Microsoft.ML.OnnxRuntime` and `Microsoft.ML.Tokenizers`.

### Changed

- The redaction policy schema moves to 1.1.0, tracking PhiSQL 1.1.0. The new schema adds the `modelPath` and
  `threshold` fields on `phEyeConfiguration`, produced by the PhiSQL `MODEL` clause.
- The remote PhEye service remains the default. When `ModelPath` is unset, behavior is unchanged from 1.0.0; setting it
  is what switches a PhEye filter to local inference.

## 1.0.0

Initial release.

- A .NET port of [Phileas (Java)](https://github.com/philterd/phileas): a library to deidentify and redact PII and PHI
  from text using configurable, policy-based filters.
- Policy-driven filters for common identifiers (names, email addresses, phone numbers, SSNs, credit cards, IP and MAC
  addresses, passport and tracking numbers, US state abbreviations, street addresses, and more).
- Replacement strategies (redact, mask, static replacement, and others), filter conditions, and ignored terms and
  patterns.
- Remote PhEye filter for AI-powered named entity recognition via the [PhEye](https://github.com/philterd/pheye) service.
- Redaction policies defined and validated against the canonical PhiSQL redaction policy schema (1.0.0).
