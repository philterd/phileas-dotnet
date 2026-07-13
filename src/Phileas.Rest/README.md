# Phileas.Rest — "lite Philter"

A cross-platform REST service that wraps the [Phileas](../Phileas) library to detect and redact PII in
**plain text, Word (.docx), Excel (.xlsx), and PDF** documents. It is a lean, redaction-only alternative to
Philter — no UI or ledger — with policy and context management backed by MongoDB and a Valkey context cache.

It is a single detection engine: all document types are redacted in-process by Phileas, so referential
integrity (consistent RANDOM_REPLACE replacements within a context) is shared across every document type.

## Endpoints

Following the [Philter filtering API](https://philterd.github.io/philter/latest/api_and_sdks/api/filtering_api/)
convention, `/filter` takes the **raw document as the request body** and selects the handler from the
`Content-Type` header. Policy and context are the `p` and `c` query parameters (defaulting to `default` and
`none`). The response carries an `x-document-id` header.

| Method | Path | Purpose |
| ------ | ---- | ------- |
| `POST` | `/filter?p=&c=` | Redact a document. Body = raw document; `Content-Type` selects text/docx/xlsx/pdf. |
| `POST` | `/explain?p=&c=` | Redact plain text and return the applied/ignored spans (`text/plain` only). |
| `GET/PUT/DELETE` | `/policies[/{name}]` | Manage policies (canonical Phileas policy JSON). |
| `GET/POST/DELETE` | `/contexts[/{name}]` | Manage contexts and their token→replacement entries. |
| `GET` | `/health` | Liveness/readiness (pings Mongo + Valkey). |
| `GET` | `/swagger` | OpenAPI UI. |

Supported `Content-Type` values for `/filter`: `text/plain`, `application/pdf`,
`application/vnd.openxmlformats-officedocument.wordprocessingml.document` (docx), and
`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` (xlsx). Any other type returns `415`.

For xlsx, an optional `headerContext=true|false` query parameter controls whether each column's header is
used as leading detection context for its data cells (e.g. an `SSN` header helps flag a bare number below it).
When omitted it falls back to the `Xlsx:UseHeaderContext` configured default. Ignored for non-xlsx types.

Example:

```bash
# Store a policy.
curl -X PUT localhost:8080/policies/default -H 'content-type: application/json' \
  -d '{"json":"{\"identifiers\":{\"emailAddress\":{}}}"}'

# Redact plain text (body is the raw text).
curl -X POST 'localhost:8080/filter?p=default&c=ctx' \
  -H 'content-type: text/plain' \
  --data 'Email me at a@b.com'

# Explain: redacted text plus the detected spans.
curl -X POST 'localhost:8080/explain?p=default&c=ctx' \
  -H 'content-type: text/plain' \
  --data 'Email me at a@b.com'

# Redact a Word document (body is the raw file).
curl -X POST 'localhost:8080/filter?p=default&c=ctx' \
  -H 'content-type: application/vnd.openxmlformats-officedocument.wordprocessingml.document' \
  --data-binary @report.docx -o redacted.docx

# Redact a PDF.
curl -X POST 'localhost:8080/filter?p=default&c=ctx' \
  -H 'content-type: application/pdf' \
  --data-binary @report.pdf -o redacted.pdf
```

## Configuration (`Phileas` section / env vars)

| Setting | Env var | Default |
| ------- | ------- | ------- |
| `MongoConnectionString` | `Phileas__MongoConnectionString` | `mongodb://localhost:27017` |
| `MongoDatabase` | `Phileas__MongoDatabase` | `phileas` |
| `ValkeyConnectionString` | `Phileas__ValkeyConnectionString` | `localhost:6379` (empty ⇒ cache-less) |
| `ContextCacheTtlSeconds` | `Phileas__ContextCacheTtlSeconds` | `3600` |
| `PhEyeModelPath` | `Phileas__PhEyeModelPath` | *(empty)* — path to the local GLiNER model directory |
| `Ocr:Mode` | `Phileas__Ocr__Mode` | `Off` — `Off`, `Fallback`, or `Always` |
| `Ocr:Language` | `Phileas__Ocr__Language` | `eng` (e.g. `eng+fra`) |
| `Ocr:TessDataPath` | `Phileas__Ocr__TessDataPath` | `/usr/share/tesseract-ocr/5/tessdata` |
| `Ocr:Dpi` | `Phileas__Ocr__Dpi` | `300` |
| `Xlsx:UseHeaderContext` | `Phileas__Xlsx__UseHeaderContext` | `true` — default for the `headerContext` query param |

`PhEyeModelPath` is injected into any policy's PhEye configuration that doesn't specify its own `modelPath`,
so authored policies stay portable. The Docker image bakes in
[`philterd/ph-eye-pii-en-small`](https://huggingface.co/philterd/ph-eye-pii-en-small) and points this at it.
The GLiNER model is loaded once at startup and shared across all requests.

### OCR (scanned / image-only PDFs)

By default PDF redaction uses only the PDF text layer, so a scanned/image-only page yields no text and isn't
redacted. Set `Ocr:Mode` to enable Tesseract OCR:

- `Fallback` — use the text layer, and OCR only the pages that have no extractable text (recommended).
- `Always` — OCR every page, ignoring any text layer.

OCR needs the native Tesseract library and `tessdata` language files; the Docker image installs
`tesseract-ocr` + `tesseract-ocr-eng` and defaults `Ocr:Mode=Fallback`. Add languages by installing more
`tesseract-ocr-<lang>` packages and extending `Ocr:Language` (e.g. `eng+fra`).

## Docker

```bash
docker build -t phileas-rest -f Dockerfile .   # build context is the repo root
docker run -p 8080:8080 \
  -e Phileas__MongoConnectionString=mongodb://host.docker.internal:27017 \
  -e Phileas__ValkeyConnectionString=host.docker.internal:6379 \
  phileas-rest
```
