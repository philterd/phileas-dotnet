# Release Notes

All notable changes to Phileas (.NET) are recorded here. Versions follow [Semantic Versioning](https://semver.org/).

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

### Changed

- The PhiSQL dependency is now 1.1.0, which defines the `validator` field in the redaction policy schema.

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
