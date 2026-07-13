# REST Service

`Phileas.Rest` is a cross-platform REST service that wraps the phileas-dotnet library so PII detection and
redaction can be consumed over HTTP. It is a lean, redaction-only service — a "lite Philter" with no UI or
ledger — that redacts **plain text, Word (`.docx`), Excel (`.xlsx`), and PDF** documents, with policy and
context management backed by MongoDB and a Valkey cache.

It is a single detection engine: every document type is redacted in-process by phileas-dotnet, so referential
integrity (consistent random replacements within a context) is shared across all document types.

## Running the service

The service needs a MongoDB instance (durable store for policies, contexts, and context entries) and,
optionally, a Valkey instance (context cache). The provided `docker-compose.yml` brings up all three:

```bash
docker compose up --build
```

Then browse the OpenAPI UI at [http://localhost:8080/swagger](http://localhost:8080/swagger).

To run the container against your own MongoDB/Valkey:

```bash
docker build -t phileas-rest -f Dockerfile .   # build context is the repo root
docker run -p 8080:8080 \
  -e Phileas__MongoConnectionString=mongodb://host.docker.internal:27017 \
  -e Phileas__ValkeyConnectionString=host.docker.internal:6379 \
  phileas-rest
```

The Docker image bakes in the [`philterd/ph-eye-pii-en-small`](https://huggingface.co/philterd/ph-eye-pii-en-small)
GLiNER model for local AI entity detection and installs Tesseract for OCR, so no external NER or OCR service is
required.

## The filtering API

The API follows the [Philter filtering API](https://philterd.github.io/philter/latest/api_and_sdks/api/filtering_api/)
convention: a single `POST /filter` whose **request body is the raw document** and whose `Content-Type` header
selects the handler. The response is the redacted document, plus an `x-document-id` response header.

### Query parameters

| Parameter | Meaning | Default |
| --------- | ------- | ------- |
| `p` | Policy name (must exist; see [managing policies](#managing-policies)) | `default` |
| `c` | Context name for referential-integrity replacements | `none` |
| `d` | Document identifier (returned in the `x-document-id` header) | auto-generated |
| `headerContext` | *(xlsx only)* use each column's header as detection context | `Xlsx:UseHeaderContext` (`true`) |

### Supported content types

| `Content-Type` | Redacts |
| -------------- | ------- |
| `text/plain` | Plain text |
| `application/pdf` | PDF |
| `application/vnd.openxmlformats-officedocument.wordprocessingml.document` | Word `.docx` |
| `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | Excel `.xlsx` |

Any other content type returns `415 Unsupported Media Type`.

### Examples

Store a policy (see [managing policies](#managing-policies)), then filter documents:

```bash
# Store a policy that redacts email addresses.
curl -X PUT localhost:8080/policies/default -H 'content-type: application/json' \
  -d '{"json":"{\"identifiers\":{\"emailAddress\":{}}}"}'

# Redact plain text (body is the raw text).
curl -X POST 'localhost:8080/filter?p=default&c=ctx' \
  -H 'content-type: text/plain' \
  --data 'Email me at a@b.com'

# Redact a Word document (body is the raw file).
curl -X POST 'localhost:8080/filter?p=default&c=ctx' \
  -H 'content-type: application/vnd.openxmlformats-officedocument.wordprocessingml.document' \
  --data-binary @report.docx -o redacted.docx

# Redact an Excel workbook, disabling header context for this request.
curl -X POST 'localhost:8080/filter?p=default&c=ctx&headerContext=false' \
  -H 'content-type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' \
  --data-binary @data.xlsx -o redacted.xlsx

# Redact a PDF.
curl -X POST 'localhost:8080/filter?p=default&c=ctx' \
  -H 'content-type: application/pdf' \
  --data-binary @report.pdf -o redacted.pdf
```

## Explaining detections

`POST /explain` redacts **plain text only** and returns the detected spans as JSON, so you can inspect what was
found and how it was replaced. It accepts the same `p`, `c`, and `d` query parameters.

```bash
curl -X POST 'localhost:8080/explain?p=default&c=ctx' \
  -H 'content-type: text/plain' \
  --data 'Email me at a@b.com'
```

```json
{
  "filteredText": "Email me at {{{REDACTED-email-address}}}",
  "context": "ctx",
  "documentId": "…",
  "explanation": {
    "appliedSpans": [
      { "characterStart": 12, "characterEnd": 21, "text": "a@b.com",
        "replacement": "{{{REDACTED-email-address}}}", "classification": null, "confidence": 1.0 }
    ],
    "ignoredSpans": []
  }
}
```

## Managing policies

Policies are stored in MongoDB as canonical Phileas policy JSON, keyed by name. See
[Policies](policies.md) for the policy format.

| Method & path | Purpose |
| ------------- | ------- |
| `GET /policies` | List policy names |
| `GET /policies/{name}` | Get a policy's JSON |
| `PUT /policies/{name}` | Create or replace a policy (body: `{"json": "<policy json>"}`) — the body is validated |
| `DELETE /policies/{name}` | Delete a policy |

```bash
curl -X PUT localhost:8080/policies/medical -H 'content-type: application/json' \
  -d '{"json":"{\"identifiers\":{\"ssn\":{},\"emailAddress\":{}}}"}'

curl localhost:8080/policies                 # ["default","medical"]
curl localhost:8080/policies/medical         # the stored JSON
```

## Managing contexts

A **context** scopes referential integrity: within a context, the same token always maps to the same
replacement (for the `RANDOM_REPLACE` strategy), and that mapping is shared across every document type. The
mapping is persisted in MongoDB and cached in Valkey. See [Context Service](context-service.md).

| Method & path | Purpose |
| ------------- | ------- |
| `GET /contexts` | List context names |
| `POST /contexts/{name}` | Register a context |
| `GET /contexts/{name}/entries` | Get the token → replacement entries for a context |
| `DELETE /contexts/{name}` | Delete a context and all its entries |
| `DELETE /contexts/{name}/entries/{token}` | Delete a single mapping |

## Excel header context

For `.xlsx` files, each cell is normally detected in isolation. Enabling **header context** prepends a
column's header (first-row) text to each data cell before detection — so, for example, an `SSN` column header
helps the detector flag a bare number in the cells below it. The detected spans are mapped back onto the
cell's own text, so only the cell (never the header) is redacted.

Control it per request with the `headerContext=true|false` query parameter, or set the deployment default with
`Phileas__Xlsx__UseHeaderContext` (default `true`).

## OCR for scanned / image PDFs

By default, PDF redaction uses only the PDF text layer, so a scanned/image-only page yields no text and is not
redacted. Set `Ocr:Mode` to enable Tesseract OCR:

| `Ocr:Mode` | Behavior |
| ---------- | -------- |
| `Off` | No OCR — text layer only (default in code) |
| `Fallback` | Text layer, and OCR only the pages that have no extractable text (recommended) |
| `Always` | OCR every page, ignoring any text layer |

The Docker image installs `tesseract-ocr` + English tessdata and defaults `Ocr:Mode=Fallback`. Add languages by
installing more `tesseract-ocr-<lang>` packages and extending `Ocr:Language` (e.g. `eng+fra`).

## Configuration

Settings are bound from the `Phileas` configuration section (`appsettings.json`) or environment variables:

| Setting | Environment variable | Default |
| ------- | -------------------- | ------- |
| `MongoConnectionString` | `Phileas__MongoConnectionString` | `mongodb://localhost:27017` |
| `MongoDatabase` | `Phileas__MongoDatabase` | `phileas` |
| `ValkeyConnectionString` | `Phileas__ValkeyConnectionString` | `localhost:6379` (empty ⇒ cache-less) |
| `ContextCacheTtlSeconds` | `Phileas__ContextCacheTtlSeconds` | `3600` |
| `PhEyeModelPath` | `Phileas__PhEyeModelPath` | *(empty)* — path to the local GLiNER model directory |
| `Ocr:Mode` | `Phileas__Ocr__Mode` | `Off` (`Off` \| `Fallback` \| `Always`) |
| `Ocr:Language` | `Phileas__Ocr__Language` | `eng` |
| `Ocr:TessDataPath` | `Phileas__Ocr__TessDataPath` | `/usr/share/tesseract-ocr/5/tessdata` |
| `Ocr:Dpi` | `Phileas__Ocr__Dpi` | `300` |
| `Xlsx:UseHeaderContext` | `Phileas__Xlsx__UseHeaderContext` | `true` |

`PhEyeModelPath` is injected into any policy's PhEye configuration that doesn't specify its own `modelPath`, so
authored policies stay portable. The GLiNER model is loaded once at startup and shared across all requests.

## Philter compatibility

The service also exposes a Philter-compatible API so it can be used as a redaction engine by Philter clients —
notably [philter-router](https://github.com/philterd/philter-router) via
[philter-sdk-java](https://github.com/philterd/philter-sdk-java) — without changes:

| Method & path | Purpose |
| ------------- | ------- |
| `POST /api/filter` | Redact text or a file. Query params `c`, `p`, `filename`, `async`. |
| `GET /api/health` / `GET /api/status` | Philter `StatusResponse` for liveness checks. |

The SDK sends the raw document as the request body and always sets `Content-Type: application/pdf` for files,
conveying the real type through the `filename` query parameter — so `/api/filter` determines the document type
from the **filename extension** (`.txt`, `.docx`, `.xlsx`, `.pdf`), and treats a request with no `filename` as
plain text. The redacted document is returned in the body with the assigned id in the `x-document-id` header.
`async` is accepted for wire compatibility but the service always filters synchronously.

To point philter-router at this service, add it as an engine in the router's configuration:

```yaml
engines:
  phileas-dotnet: http://phileas-rest:8080
```

## Operational endpoints

| Method & path | Purpose |
| ------------- | ------- |
| `GET /health` | Liveness/readiness — pings MongoDB and Valkey; `503` if either is unreachable |
| `GET /api/health`, `GET /api/status` | Philter-compatible liveness (see [Philter compatibility](#philter-compatibility)) |
| `GET /swagger` | OpenAPI UI |

## Security

The service ships with **no authentication** by design. Run it network-isolated — on a private network or
behind an authenticating gateway — and do not expose it directly to untrusted networks.

## Next Steps

- [Policies](policies.md) — the policy format used by `PUT /policies`
- [Supported Identifiers](supported-identifiers.md) — the PII types a policy can detect
- [PhEye Filter Usage](pheye-filter-usage.md) — AI entity detection (local GLiNER or remote PhEye)
- [Context Service](context-service.md) — how referential integrity works
- [PDF Redaction](pdf-redaction.md) and [Word & Excel Redaction](office-redaction.md) — the document redactors behind the API
