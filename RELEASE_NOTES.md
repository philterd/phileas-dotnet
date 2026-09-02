# Release Notes

All notable changes to Phileas (.NET) are recorded here. Versions follow [Semantic Versioning](https://semver.org/). The current development version is `1.7.0-preview`; `1.6.0` is the latest published release.

## 1.7.0-preview

_Unreleased._

Built on PhiSQL 1.3.0.

### Changed

- **The `Philterd.PhiSql` dependency is now 1.3.0** (up from 1.2.0), moving policy operations to redaction policy
  schema 1.3.0. It adds an optional top-level `metadata` object and makes the plural `zipCodeFilterStrategies` the
  canonical key; the singular `zipCodeFilterStrategy` is deprecated but still validates.

### Removed

- **The `PhysicianName` filter type**, following PhiSQL 1.3.0's removal of the `PHYSICIAN_NAME` entity type. It was
  an unused enum member here (no policy field, filter, or strategy), so only code naming it breaks. Use PhEye
  detection instead: `DETECT PHEYE LABELS ('physician name') WITH REDACT;`.

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
