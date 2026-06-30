# Release Notes

All notable changes to Phileas (.NET) are recorded here. Versions follow [Semantic Versioning](https://semver.org/). The current development version is `1.5.0-preview`; `1.4.0` is the latest published release.

## 1.5.0-preview

### Added

- **`FilterService` now accepts an injected `IContextService`** (constructor overload), so consistent RANDOM_REPLACE replacements can be backed by a durable, caller-supplied store instead of only the default in-memory one.

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
