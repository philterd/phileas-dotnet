# PDF Redaction

phileas-dotnet can redact PII directly in **PDF documents**. It detects sensitive values using the same
policies and filters used for text, then produces a redacted document in which every page is rendered to an
image with redaction rectangles burned in.

> **No recoverable text.** Because each page is rasterized, the redacted output has **no text layer at all** —
> none of the original text (PII or otherwise) can be extracted, copied, or searched from it. This is the core
> security property of PDF redaction.

## How it works

1. **Extract** — text is extracted line-by-line from the PDF, keeping the position (bounding box) of every
   character. This step is **pluggable** (see [Custom text extraction](#custom-text-extraction-itextextractor)):
   by default the PDF text layer is read, but any `ITextExtractor` — for example one backed by OCR for scanned
   pages — can supply the lines.
2. **Detect** — each line is run through the normal [filter pipeline](supported-identifiers.md), producing
   spans. Each detected span is tagged with its page number and bounding box.
3. **Redact** — every page is rendered to a raster image at the configured DPI, a filled rectangle (and,
   optionally, replacement text) is drawn over each detected span and each [graphical bounding
   box](#graphical-bounding-boxes), and the pages are reassembled into the requested output format.

## Quick start

```csharp
using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services.Pdf;

var policy = new Policy
{
    Name = "pdf-policy",
    Identifiers = new Identifiers
    {
        Ssn = new Ssn(),
        Date = new Date(),
        ZipCode = new ZipCode()
    }
};

byte[] inputPdf = File.ReadAllBytes("input.pdf");

var result = new PdfFilterService().Filter(
    policy,
    context: "default",
    input: inputPdf,
    outputMimeType: MimeType.ApplicationPdf);

File.WriteAllBytes("redacted.pdf", result.Document);

Console.WriteLine($"Redacted {result.Spans.Count} spans.");
```

## `PdfFilterService`

`PdfFilterService` (in `Phileas.Services.Pdf`) is the entry point.

### Filter

```csharp
public BinaryDocumentFilterResult Filter(
    Policy policy,
    string context,
    byte[] input,
    MimeType outputMimeType)
```

Detects PII in `input` and returns the redacted document plus the detected spans.

| Parameter | Type | Description |
|---|---|---|
| `policy` | `Policy` | The policy defining which identifiers to detect and how to replace them. |
| `context` | `string` | A named scope (as for text filtering). |
| `input` | `byte[]` | The source PDF bytes. |
| `outputMimeType` | `MimeType` | The desired output format (see [Output formats](#output-formats)). |

**Returns** a [`BinaryDocumentFilterResult`](#binarydocumentfilterresult).

### Apply

```csharp
public byte[] Apply(Policy policy, byte[] input, IList<Span> spans, MimeType outputMimeType)
```

Redacts `input` using a pre-computed set of spans (which must already carry their page number and
coordinates — for example, the spans returned by a prior `Filter` call). Returns the redacted bytes.

## Output formats

`MimeType` (in `Phileas.Model`) selects the output:

| Value | Output |
|---|---|
| `MimeType.ApplicationPdf` | A redacted PDF whose pages are rasterized images (no text layer). |
| `MimeType.ImageJpeg` | A ZIP archive containing one redacted JPEG image per page (`page-0.jpeg`, `page-1.jpeg`, …). |

## BinaryDocumentFilterResult

```csharp
public class BinaryDocumentFilterResult
{
    public byte[] Document { get; }
    public string Context { get; }
    public IList<Span> Spans { get; }
    public long Tokens { get; }
}
```

| Property | Type | Description |
|---|---|---|
| `Document` | `byte[]` | The redacted output bytes (a PDF, or a ZIP of images, per the requested format). |
| `Context` | `string` | The context name passed to `Filter`. |
| `Spans` | `IList<Span>` | The detected spans, each carrying its `PageNumber` and bounding box (`LowerLeftX/Y`, `UpperRightX/Y`). |
| `Tokens` | `long` | The number of whitespace-delimited tokens in the source document. |

## Custom text extraction (`ITextExtractor`)

By default `PdfFilterService` reads the PDF's **text layer** (via `PdfTextExtractor`, backed by PdfPig).
The extraction step is pluggable: `PdfFilterService` accepts any `ITextExtractor`, so positioned lines can
come from **any source** — most notably **OCR of scanned pages**, which have no text layer to read.

```csharp
public PdfFilterService(FilterService? filterService = null, ITextExtractor? textExtractor = null)
```

An extractor returns positioned lines; each character carries a library-independent bounding box in PDF
user-space points (bottom-left origin), so detected spans can be located back on the page:

```csharp
public interface ITextExtractor
{
    IReadOnlyList<PdfLine> GetLines(byte[] document);
}

public sealed class PdfLine
{
    public PdfLine(int pageNumber, string text, IReadOnlyList<CharBox?> charBoxes);
    public int PageNumber { get; }
    public string Text { get; }
    public IReadOnlyList<CharBox?> CharBoxes { get; } // null entries are synthesized word separators
}

public readonly struct CharBox  // PDF user-space points, bottom-left origin
{
    public CharBox(double left, double bottom, double right, double top);
    public double Left { get; }
    public double Bottom { get; }
    public double Right { get; }
    public double Top { get; }
}
```

Supply your own extractor to redact scanned PDFs or to integrate an alternative text source. The rest of
the pipeline (detect → locate → redact) is unchanged regardless of where the lines came from:

```csharp
var service = new PdfFilterService(filterService: null, textExtractor: new MyOcrTextExtractor());
var result = service.Filter(policy, "ctx", scannedPdf, MimeType.ApplicationPdf);
```

phileas-dotnet does **not** ship an OCR implementation; OCR is provided by the host application (for
example, Philter Desktop uses the operating system's on-device OCR to read scanned pages).

> **Added in 1.4.0.** `PdfLine` now carries `CharBox` (previously the PdfPig `Letter`), decoupling
> extraction from the PDF text layer so OCR and other sources can feed the same redaction pipeline. This
> is a breaking change for code that constructed `PdfLine` or read its per-character glyphs; see the
> release notes.

## Configuration

PDF rendering is controlled by `Config.Pdf` on the policy (see [Policies — Config](policies.md#config)).

```csharp
var policy = new Policy
{
    Name = "pdf-policy",
    Config = new Config
    {
        Pdf = new Pdf
        {
            RedactionColor = "black",
            Dpi = 200,
            ShowReplacement = false
        }
    },
    Identifiers = new Identifiers { Ssn = new Ssn() }
};
```

| Property | JSON key | Default | Description |
|---|---|---|---|
| `RedactionColor` | `redactionColor` | `"black"` | Policy-wide fill color of the redaction bars, used for any span whose strategy does not set its own [`color`](filter-strategies.md#redaction-bar-color). Accepts a named color (`black`, `white`, `red`, `orange`, `yellow`, `green`, `blue`, `gray`) or a 6-digit hex string like `#ff8800`; an unrecognized or malformed value renders as black. |
| `ShowReplacement` | `showReplacement` | `false` | Draw the strategy's replacement text inside the redaction rectangle. |
| `ReplacementFont` | `replacementFont` | `"helvetica"` | Font for replacement text (`helvetica`, `times`, `courier`). |
| `ReplacementMaxFontSize` | `replacementMaxFontSize` | `12` | Maximum replacement-text font size (shrinks to fit the box). |
| `ReplacementFontColor` | `replacementFontColor` | `null` (white) | Replacement-text color. |
| `Dpi` | `dpi` | `150` | Resolution at which pages are rasterized. |
| `Scale` | `scale` | `0.25` | Output page size as a fraction of the original (the rasterized image keeps full DPI). Set to `1.0` for original-size pages. |
| `CompressionQuality` | `compressionQuality` | `1.0` | JPEG quality (0–1) of the embedded page images. |
| `PreserveUnredactedPages` | `preserveUnredactedPages` | `false` | *(Not yet implemented in the .NET port — all pages are rasterized.)* |

> **Note on `Scale`.** The default (`0.25`) produces quarter-size pages backed by a high-resolution image,
> compact output that stays crisp when zoomed. Set `Scale = 1.0f` to keep the original page dimensions.

> **Per-strategy bar color.** A filter strategy can set its own [`color`](filter-strategies.md#redaction-bar-color) to override `RedactionColor` for the spans it redacts, so different entity types (or the same type at different confidences) can be redacted in different colors. The resolution order for each span's bar is the strategy's `color`, then `RedactionColor`, then black.

## Graphical bounding boxes

In addition to detected PII, you can redact **fixed rectangular regions** regardless of content — useful for
signatures, logos, or known sensitive areas. These are defined on `policy.Graphical.BoundingBoxes`:

```csharp
var policy = new Policy
{
    Name = "graphical",
    Graphical = new Graphical
    {
        BoundingBoxes = new List<BoundingBox>
        {
            // A box in PDF user-space points (origin at the page's lower-left).
            new BoundingBox { Page = 1, X = 72, Y = 72, W = 200, H = 50, Color = "black" }
        }
    },
    Identifiers = new Identifiers()
};

var result = new PdfFilterService().Filter(policy, "ctx", inputPdf, MimeType.ApplicationPdf);
```

| Property | JSON key | Description |
|---|---|---|
| `Page` | `page` | Page the box covers: a 1-based page number (default `1`), `0` for **every** page, or a negative value `-N` for page N through the **last** page (so `-2` is "all but the first page"). |
| `X`, `Y` | `x`, `y` | Lower-left corner, in PDF user-space points. |
| `W`, `H` | `w`, `h` | Width and height, in points. |
| `Color` | `color` | Box color (a named color or 6-digit hex, same set as `RedactionColor`; unrecognized renders black), or `null` to use the policy `RedactionColor`. |

## Notes and limitations

- **Rasterized output.** All pages become images. This guarantees no recoverable text, but the output is
  larger than the source and is not text-searchable.
- **Scanned / image-only PDFs.** A page with no text layer yields nothing from the default extractor, so
  nothing is detected or redacted on it. To redact scanned pages, supply an OCR-backed `ITextExtractor`
  (see [Custom text extraction](#custom-text-extraction-itextextractor)); phileas-dotnet does not include
  OCR itself.
- **Native dependencies.** PDF rendering uses PDFium (via PDFtoImage) and SkiaSharp, which include native
  binaries. On Linux you may need the appropriate `SkiaSharp.NativeAssets.Linux*` package for your
  deployment. See the [NOTICE](https://www.github.com/philterd/phileas-dotnet/blob/main/NOTICE) file for the full
  dependency and license list.
- **`PreserveUnredactedPages`** is accepted in the policy but not yet honored in the .NET port; every page is
  rasterized.

## See Also

- [Supported Identifiers](supported-identifiers.md) — the PII types detected in the PDF text
- [Filter Strategies](filter-strategies.md) — how detected PII is replaced
- [Policies](policies.md) — the `Config.Pdf` and `Graphical` options
