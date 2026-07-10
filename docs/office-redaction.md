# Word & Excel Redaction

phileas-dotnet can redact PII directly in **Microsoft Word (`.docx`)** and **Excel (`.xlsx`)** documents. It
detects sensitive values using the same policies and filters used for text, then rewrites the Open XML
package in place so the redacted output keeps its structure and formatting.

Redaction is performed with the open-source [Open XML SDK](https://github.com/dotnet/Open-XML-SDK)
(`DocumentFormat.OpenXml`) — no commercial dependency or license key — and the services are cross-platform
(they run on Linux, macOS, and Windows).

> **Port-specific format support.** Word and Excel container-format redaction is available in the **.NET port
> only**. `phileas-java` and `phileas-python` redact **PDF** documents but not `.docx`/`.xlsx`. Phileas holds
> strict cross-port parity for *redaction semantics* (which values are detected and how they are replaced);
> *container-format support* is a separate axis and is documented per port. See [Cross-port
> parity](#cross-port-parity).

## How it works

Unlike PDF redaction (which rasterizes each page to an image), Office redaction edits the document's XML in
place, so the output remains a fully editable `.docx`/`.xlsx`:

1. **Enumerate** — the document is walked in a stable, canonical order: for Word, each body paragraph (then
   headers/footers, footnotes, endnotes, and comments); for Excel, each cell (workbook sheet order, then rows,
   then cells). This order defines the paragraph/cell index used to re-apply spans.
2. **Detect** — each unit of text is run through the normal [filter pipeline](supported-identifiers.md),
   producing spans.
3. **Rewrite** — when a paragraph or cell changed, it is rebuilt from the filtered text (Word preserves the
   surrounding run/drawing structure; Excel writes the cell back as an inline string). Additional passes cover
   text that is not a body paragraph/cell (see [What gets redacted](#what-gets-redacted)).

Because the filter is supplied as a delegate, detection uses whatever policy, context, and identifiers you
configure — identical to text and PDF filtering.

## Quick start

The redactors take a **filter delegate** — `Func<string, TextFilterResult>` — that you build from a
[`FilterService`](getting-started.md) and your policy. The same delegate shape works for Word and Excel.

### Word (`.docx`)

```csharp
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Phileas.Services.Office;

var policy = new Policy
{
    Name = "office-policy",
    Identifiers = new Identifiers
    {
        Ssn = new Ssn(),
        EmailAddress = new EmailAddress()
    }
};

var filterService = new FilterService();
Func<string, TextFilterResult> filter = text => filterService.Filter(policy, "default", 0, text);

List<OfficeRedactionSpan> spans = WordDocumentRedactor.Redact("input.docx", "redacted.docx", filter);

Console.WriteLine($"Redacted {spans.Count} spans.");
```

### Excel (`.xlsx`)

```csharp
List<OfficeRedactionSpan> spans = XlsxRedactor.Redact("input.xlsx", "redacted.xlsx", filter);
```

The input file is never modified: each redactor builds the whole output in memory and writes it once, so a
failure never leaves a partial or corrupted file.

### In-memory (`byte[]`) overloads

Every operation has a `byte[]` overload alongside the file-path one, so a service can redact document bytes
end-to-end without touching the file system. `Redact` returns the redacted bytes plus the spans; `ApplySpans`
returns the redacted bytes; `Detect` and the `Read*` helpers take bytes directly. The input array is copied,
never mutated.

```csharp
byte[] input = /* the uploaded .docx bytes */;

(byte[] redacted, List<OfficeRedactionSpan> spans) =
    WordDocumentRedactor.Redact(input, filter);

// return `redacted` in the HTTP response; report `spans.Count`
```

```csharp
(byte[] redacted, List<OfficeRedactionSpan> spans) = XlsxRedactor.Redact(xlsxBytes, filter);
```

The file-path overloads simply read the input, call the `byte[]` core, and write the result — so both paths
produce identical output.

## `WordDocumentRedactor`

`WordDocumentRedactor` (in `Phileas.Services.Office`) is a static entry point for `.docx`.

| Method | Description |
|---|---|
| `Redact(inputPath, outputPath, filter, highlight = false, redactHeadersFooters = true, redactCharts = true, removeEmbeddedObjects = true)` | Detects and redacts, writes the output, and returns the applied spans. |
| `Detect(inputPath, filter, redactHeadersFooters = true, redactCharts = true)` | Detects only (no file is written); returns the spans `Redact` would apply. Used for previews and post-redaction verification. |
| `ApplySpans(inputPath, outputPath, spans, highlight, drawingFilter = null, redactCharts = true, removeEmbeddedObjects = true)` | Applies an explicit set of spans (detected or user-supplied) by **position** (paragraph index + character offsets). Supply `drawingFilter` to also re-redact non-positional content (drawings, hyperlink targets, field instructions, charts) via the policy. |
| `ReadParagraphs(inputPath)` | Returns each redactable paragraph's text in canonical order (index `i` is `ParagraphIndex` `i`). Read-only. |
| `ReadReviewLines(inputPath)` | Returns every readable line — paragraphs plus shape/SmartArt/chart text — for a before/after review diff. Read-only. |

```csharp
public static List<OfficeRedactionSpan> Redact(
    string inputPath,
    string outputPath,
    Func<string, TextFilterResult> filter,
    bool highlight = false,
    bool redactHeadersFooters = true,
    bool redactCharts = true,
    bool removeEmbeddedObjects = true);
```

Setting `highlight = true` wraps each replacement in a yellow highlight run so redactions are visible in the
output document.

## `XlsxRedactor`

`XlsxRedactor` (in `Phileas.Services.Office`) is the static entry point for `.xlsx`.

| Method | Description |
|---|---|
| `Redact(inputPath, outputPath, filter, fullyRedactedColumns = null, worksheet = null, redactHeadersFooters = true, redactCharts = true, redactFormulaValues = true, redactPivotCaches = true, removeUninspectableEmbeddedObjects = true)` | Detects and redacts, writes the output, and returns the applied spans. |
| `Detect(inputPath, filter, worksheet = null, redactHeadersFooters = true, redactCharts = true)` | Detects only (no file is written). |
| `ApplySpans(inputPath, outputPath, spans, worksheet = null, policyFilter = null, …)` | Applies an explicit set of cell-indexed spans by position. Supply `policyFilter` to also re-redact non-cell content. |
| `ReadSheetNames(inputPath)` | The worksheet names in workbook order. |
| `ReadColumns(inputPath, sheetName = null)` | The columns of a worksheet (letter + header) for a "redact entire column" picker. Returns `SpreadsheetColumn`. |

**Redact-entire-column.** Pass 1-based column indices in `fullyRedactedColumns` to clear every data cell in
those columns outright (the header row is preserved), regardless of whether a detector matches — useful for a
column of names the model may not flag in isolation. Cleared cells are replaced with
`XlsxRedactor.ColumnReplacement` and classified `XlsxRedactor.ColumnClassification`.

**Single worksheet.** Pass a `worksheet` name to limit redaction to one sheet; omit it (or pass empty) to
redact the whole workbook.

## `OfficeRedactionSpan`

Both redactors return, and `ApplySpans` consumes, a list of `OfficeRedactionSpan` — a persistence-free record
of each redaction (the Office analog of `Span` on the PDF path). A consuming application maps it to its own
storage type; the library never persists it.

```csharp
public sealed class OfficeRedactionSpan
{
    public int Order { get; set; }           // stable order within a redaction pass
    public int ParagraphIndex { get; set; }  // docx paragraph / xlsx cell ordinal; -1 for non-paragraph content
    public int CharacterStart { get; set; }
    public int CharacterEnd { get; set; }
    public string Text { get; set; }          // the original (detected) text
    public string Replacement { get; set; }
    public string Classification { get; set; }
    // Explanation detail, copied from the engine Span at capture time:
    public string FilterType { get; set; }
    public double Confidence { get; set; }
    public string Pattern { get; set; }
    public List<string> Window { get; set; }
}
```

A `ParagraphIndex` of `-1` marks content that is **not** a body paragraph or cell — a drawing/chart label, a
comment, a header/footer, a hyperlink target, a field instruction, a tracked deletion, a pivot-cache value, or
an embedded object. Such spans are reported (so they appear in a redaction report) but are re-applied by the
policy filter rather than by position.

## What gets redacted

Office documents hide copies of text in many places besides the visible body; the redactors cover them so PII
does not survive in a part the eye never sees.

**Word (`.docx`):**

- Body paragraphs, including tables
- Headers and footers *(toggle: `redactHeadersFooters`)*
- Footnotes, endnotes, and comments (including modern threaded comments)
- Shape, text-box, and SmartArt text (DrawingML)
- Charts — title/axis/label text and cached series/category values *(toggle: `redactCharts`)*
- Hyperlink URL **targets** (the address behind a link, not just its visible text)
- Field instructions (`HYPERLINK`, `INCLUDETEXT`, mail-merge sources)
- Tracked **deletions** (`w:delText`) — text recoverable via "Reject Changes"
- Embedded Word/Excel objects (redacted in place); opaque OLE objects are removed or kept-and-flagged *(toggle: `removeEmbeddedObjects`)*

**Excel (`.xlsx`):**

- Text and number/date cells (a bare number can be an SSN, phone, or account number)
- The shared-string table — orphaned entries left by redacted cells are blanked so the original can't be
  recovered from `xl/sharedStrings.xml`
- Print headers/footers *(toggle: `redactHeadersFooters`)*
- Cell comments (legacy and threaded) and threaded-comment author names
- Worksheet shapes and text boxes
- Charts — label text and cached plotted values, plus each chart's embedded source workbook *(toggle: `redactCharts`)*
- Cached formula results, which can duplicate a now-redacted value; the caches are cleared and the workbook is
  set to recalculate on open *(toggle: `redactFormulaValues`)*
- Pivot caches — a denormalized copy of the source data *(toggle: `redactPivotCaches`)*
- Embedded objects *(toggle: `removeUninspectableEmbeddedObjects`)*

## Notes and limitations

- **Editable output.** Unlike PDF redaction, the output stays a real, editable `.docx`/`.xlsx` — the redacted
  text is replaced in the XML, not painted over. There is no image rasterization step.
- **Changed paragraphs are flattened.** When a Word paragraph is rewritten, its inline run formatting is
  flattened (and hyperlinks/fields in it collapse to plain text), since the visible text is what is redacted.
  Paragraphs with no detected PII are left exactly as they were.
- **Opaque embedded objects.** An embedded object that isn't a Word/Excel document (for example a legacy OLE
  object) can't be inspected. With the removal option on it is deleted; otherwise it is kept and flagged so the
  caller can warn that its content was not redacted.
- **Formats.** These services handle the Open XML formats (`.docx`, `.xlsx`) only — not the legacy binary
  `.doc`/`.xls` formats.

## Cross-port parity

Phileas maintains strict parity across its Java, Python, and .NET ports for **redaction semantics** — the set
of identifiers, how confidence and disambiguation work, and how each strategy replaces a detected value.
**Container-format support** is deliberately a separate axis:

| Container format | .NET | Java | Python |
|---|:---:|:---:|:---:|
| Plain text | ✅ | ✅ | ✅ |
| PDF | ✅ | ✅ | ✅ |
| Word `.docx` | ✅ | — | — |
| Excel `.xlsx` | ✅ | — | — |

Word/Excel redaction in the .NET port does not imply the Java or Python ports gain it; bringing `.docx`/`.xlsx`
to those ports (via Apache POI / openpyxl) is a separate decision.

## See Also

- [Supported Identifiers](supported-identifiers.md) — the PII types detected in the document text
- [Filter Strategies](filter-strategies.md) — how detected PII is replaced
- [Policies](policies.md) — configuring what to detect
- [PDF Redaction](pdf-redaction.md) — redacting PII in PDF documents
