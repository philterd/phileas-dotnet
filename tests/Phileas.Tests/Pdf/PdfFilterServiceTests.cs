/*
 * Copyright 2026 Philterd, LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.IO.Compression;
using System.Text;
using Phileas.Model;
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;
using Phileas.Services;
using Phileas.Services.Pdf;
using SkiaSharp;
using UglyToad.PdfPig;
using Xunit;
using PolicyIdentifiers = Phileas.Policy.Identifiers;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests.Pdfs;

[Collection("Pdf")]
public class PdfFilterServiceTests
{
    private static byte[] SamplePdf() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Resources", "sample.pdf"));

    private static PhileasPolicy Policy() => new()
    {
        Name = "pdf",
        Identifiers = new PolicyIdentifiers
        {
            Date = new Date(),
            ZipCode = new ZipCode(),
            Currency = new Currency()
        }
    };

    private static PhileasPolicy SsnPolicy() => new()
    {
        Name = "ssn",
        Identifiers = new PolicyIdentifiers { Ssn = new Ssn() }
    };

    // An extractor that returns hand-built lines regardless of the document, standing in for any
    // non-text-layer source (e.g. OCR of a scanned page).
    private sealed class StubTextExtractor : ITextExtractor
    {
        private readonly IReadOnlyList<PdfLine> _lines;
        public StubTextExtractor(IReadOnlyList<PdfLine> lines) => _lines = lines;
        public IReadOnlyList<PdfLine> GetLines(byte[] document) => _lines;
    }

    // One 10x12 pt box per character, laid left-to-right at y [100,112]: char i -> [i*10, i*10+10].
    private static List<CharBox?> BoxesFor(string text)
    {
        var boxes = new List<CharBox?>(text.Length);
        for (var i = 0; i < text.Length; i++)
            boxes.Add(new CharBox(i * 10, 100, i * 10 + 10, 112));
        return boxes;
    }

    private static int PageCount(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return document.NumberOfPages;
    }

    // A name policy that runs on-device inference using the committed synthetic GLiNER fixture.
    private static PhileasPolicy FixtureNamePolicy() => new()
    {
        Name = "pdf-names",
        Identifiers = new PolicyIdentifiers
        {
            PhEyes = new List<PhEye>
            {
                new()
                {
                    PhEyeConfiguration = new PhEyeConfiguration
                    {
                        ModelPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Gliner"),
                        Labels = new List<string> { "person" },
                        Threshold = 0.5
                    },
                    Strategies = new List<PhEyeFilterStrategy> { new() }
                }
            }
        }
    };

    [Fact]
    public void Filter_DensePage_ChunksNameDetectionWithoutExceedingModelContext()
    {
        // sample.pdf has ~1,700 words. Per-page detection feeds each page's full text to the name model
        // at once, which far exceeds GLiNER's max_len — exercising the model's internal token-aware
        // chunking. The redaction must complete without a context-overflow error and still locate spans.
        var result = new PdfFilterService().Filter(FixtureNamePolicy(), "ctx", SamplePdf(), MimeType.ApplicationPdf);

        Assert.NotEmpty(result.Spans); // the fixture fires deterministic name spans across the chunks
        Assert.All(result.Spans, span =>
        {
            Assert.True(span.PageNumber >= 1);
            Assert.True(span.UpperRightX > span.LowerLeftX, "span should have a positive-width box");
            Assert.True(span.UpperRightY > span.LowerLeftY, "span should have a positive-height box");
        });
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(result.Document, 0, 4));
    }

    [Fact]
    public void Filter_MultiPageDocument_AssignsSpansToTheirOwnPage()
    {
        // sample.pdf spans several pages; per-page detection must keep each span on its own page
        // (the fixture name model fires on every page's text, so spans appear across pages).
        var result = new PdfFilterService().Filter(FixtureNamePolicy(), "ctx", SamplePdf(), MimeType.ApplicationPdf);

        var pages = result.Spans.Select(span => span.PageNumber).Distinct().ToList();
        Assert.True(pages.Count >= 2, $"expected spans across multiple pages, got pages: {string.Join(",", pages)}");
        Assert.Contains(2, pages); // spans are correctly attributed to page 2, not all collapsed onto page 1
    }

    [Fact]
    public void SplitSpanAcrossLines_SpanWithinOneLine_YieldsOnePortion()
    {
        // Page text "Hello world\nGoodbye" -> line 0 = [0,11), line 1 = [12,19).
        var lines = new List<(int Start, int Length)> { (0, 11), (12, 7) };

        // "world" is at page offsets [6,11), entirely on line 0.
        var portions = PdfFilterService.SplitSpanAcrossLines(6, 11, lines).ToList();

        var portion = Assert.Single(portions);
        Assert.Equal((0, 6, 11), portion); // line 0, local offsets 6..11
    }

    [Fact]
    public void SplitSpanAcrossLines_SpanStraddlingLineBreak_YieldsOnePortionPerLine()
    {
        // "...John\nSmith..." — line 0 = [0,11) ("contact John"), line 1 = [12,21) ("Smith here").
        var lines = new List<(int Start, int Length)> { (0, 11), (12, 10) };

        // The span "John\nSmith" covers page offsets [8,17): "John" on line 0, "Smith" on line 1.
        var portions = PdfFilterService.SplitSpanAcrossLines(8, 17, lines).ToList();

        Assert.Equal(2, portions.Count);
        Assert.Equal((0, 8, 11), portions[0]);  // "John" -> line 0, local 8..11
        Assert.Equal((1, 0, 5), portions[1]);   // "Smith" -> line 1, local 0..5 (newline at 11 excluded)
    }

    [Fact]
    public void Filter_LocatesSpansFromAnyTextExtractor_NotJustThePdfTextLayer()
    {
        // The 1.4.0 contract: positioned lines can come from any ITextExtractor (e.g. OCR), carrying
        // CharBox coordinates, and a detected span is located from those boxes — no PDF text layer
        // involved. Each character gets a 10x12 pt box laid left-to-right at y [100,112].
        const string text = "SSN 123-45-6789";
        var boxes = new List<CharBox?>(text.Length);
        for (var i = 0; i < text.Length; i++)
            boxes.Add(new CharBox(i * 10, 100, i * 10 + 10, 112));
        var extractor = new StubTextExtractor(new[] { new PdfLine(1, text, boxes) });

        var result = new PdfFilterService(new FilterService(), extractor)
            .Filter(SsnPolicy(), "ctx", SamplePdf(), MimeType.ApplicationPdf);

        var span = Assert.Single(result.Spans);
        Assert.Equal(1, span.PageNumber);

        // The located box is the union of the SSN characters' CharBoxes ("123-45-6789" at chars 4..14).
        var start = text.IndexOf("123", StringComparison.Ordinal);
        Assert.Equal(start * 10, span.LowerLeftX, 3);
        Assert.Equal(text.Length * 10, span.UpperRightX, 3);
        Assert.Equal(100, span.LowerLeftY, 3);
        Assert.Equal(112, span.UpperRightY, 3);

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(result.Document, 0, 4));
    }

    [Fact]
    public void Filter_SplitsAnEntityWrappedAcrossLines_IntoOneBoxPerLine()
    {
        // Two visual lines on one page. A custom identifier matches "John\s+Smith", which spans the
        // newline the per-page concatenation inserts between the lines. The located result must be split
        // into one box per line (the line-wrap behavior), both on page 1.
        var extractor = new StubTextExtractor(new[]
        {
            new PdfLine(1, "Contact John", BoxesFor("Contact John")),
            new PdfLine(1, "Smith here", BoxesFor("Smith here"))
        });

        var policy = new PhileasPolicy
        {
            Name = "wrap",
            Identifiers = new PolicyIdentifiers
            {
                CustomIdentifiers = new List<Identifier>
                {
                    new()
                    {
                        Pattern = @"John\s+Smith",
                        Classification = "name",
                        Strategies = new List<IdentifierFilterStrategy> { new() }
                    }
                }
            }
        };

        var result = new PdfFilterService(new FilterService(), extractor)
            .Filter(policy, "ctx", SamplePdf(), MimeType.ApplicationPdf);

        Assert.Equal(2, result.Spans.Count);
        Assert.All(result.Spans, s => Assert.Equal(1, s.PageNumber));
        // "John" is chars 8..11 of line 0 -> box [80,120]; "Smith" is chars 0..4 of line 1 -> [0,50].
        Assert.Equal(80, result.Spans[0].LowerLeftX, 3);
        Assert.Equal(120, result.Spans[0].UpperRightX, 3);
        Assert.Equal(0, result.Spans[1].LowerLeftX, 3);
        Assert.Equal(50, result.Spans[1].UpperRightX, 3);
    }

    [Fact]
    public void Filter_DropsSpansThatCoverNoPositionedGlyphs()
    {
        // A detected span whose characters have no boxes (e.g. a source that returned text but no
        // geometry) cannot be located, so it is dropped rather than drawn at a wrong/zero position.
        const string text = "SSN 123-45-6789";
        var noBoxes = new List<CharBox?>(new CharBox?[text.Length]); // all null
        var extractor = new StubTextExtractor(new[] { new PdfLine(1, text, noBoxes) });

        var result = new PdfFilterService(new FilterService(), extractor)
            .Filter(SsnPolicy(), "ctx", SamplePdf(), MimeType.ApplicationPdf);

        Assert.Empty(result.Spans);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(result.Document, 0, 4));
    }

    [Fact]
    public void Filter_WithNoExtractedText_ProducesNoSpansButAValidPdf()
    {
        var extractor = new StubTextExtractor(Array.Empty<PdfLine>());

        var result = new PdfFilterService(new FilterService(), extractor)
            .Filter(SsnPolicy(), "ctx", SamplePdf(), MimeType.ApplicationPdf);

        Assert.Empty(result.Spans);
        Assert.Equal(PageCount(SamplePdf()), PageCount(result.Document));
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(result.Document, 0, 4));
    }

    [Fact]
    public void Filter_DetectsSpansAndLocatesThemOnThePage()
    {
        var result = new PdfFilterService().Filter(Policy(), "ctx", SamplePdf(), MimeType.ApplicationPdf);

        Assert.NotEmpty(result.Spans);
        Assert.All(result.Spans, span =>
        {
            Assert.True(span.PageNumber >= 1);
            Assert.True(span.UpperRightX > span.LowerLeftX, "span should have a positive-width box");
            Assert.True(span.UpperRightY > span.LowerLeftY, "span should have a positive-height box");
        });
        Assert.True(result.Tokens > 0);
    }

    [Fact]
    public void RedactedPdf_HasNoRecoverableText()
    {
        var input = SamplePdf();
        var result = new PdfFilterService().Filter(Policy(), "ctx", input, MimeType.ApplicationPdf);

        // Output is a valid PDF.
        Assert.True(result.Document.Length > 0);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(result.Document, 0, 4));

        // Page count is preserved.
        Assert.Equal(PageCount(input), PageCount(result.Document));

        // The pages are rasterized images, so NO text (and therefore none of the detected PII) is
        // recoverable from the output. This is the core security property.
        using var redacted = PdfDocument.Open(result.Document);
        var recoverable = redacted.GetPages().SelectMany(p => p.Letters).Count();
        Assert.Equal(0, recoverable);
    }

    [Fact]
    public void ImageOutput_IsAZipOfOnePerPageImage()
    {
        var input = SamplePdf();
        var result = new PdfFilterService().Filter(Policy(), "ctx", input, MimeType.ImageJpeg);

        using var archive = new ZipArchive(new MemoryStream(result.Document), ZipArchiveMode.Read);

        Assert.Equal(PageCount(input), archive.Entries.Count);
        Assert.All(archive.Entries, entry =>
        {
            Assert.EndsWith(".jpeg", entry.Name);
            Assert.True(entry.Length > 0);
        });
    }

    [Fact]
    public void Apply_RedactsUsingPrecomputedSpans()
    {
        var input = SamplePdf();
        var service = new PdfFilterService();

        // Detect first, then re-apply the same spans.
        var detected = service.Filter(Policy(), "ctx", input, MimeType.ApplicationPdf).Spans;
        Assert.NotEmpty(detected);

        var redacted = service.Apply(Policy(), input, detected, MimeType.ApplicationPdf);

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(redacted, 0, 4));
        using var document = PdfDocument.Open(redacted);
        Assert.Empty(document.GetPages().SelectMany(p => p.Letters));
    }

    [Fact]
    public void GraphicalBoundingBox_IsDrawnInTheConfiguredColor()
    {
        var input = SamplePdf();

        // A red box over a fixed region of page 1, with no PII detection involved.
        var policy = new PhileasPolicy
        {
            Name = "graphical",
            Identifiers = new PolicyIdentifiers(),
            Graphical = new Phileas.Policy.Graphical
            {
                BoundingBoxes = new List<Phileas.Policy.BoundingBox>
                {
                    new() { Page = 1, X = 100, Y = 300, W = 200, H = 200, Color = "red" }
                }
            }
        };

        var result = new PdfFilterService().Filter(policy, "ctx", input, MimeType.ImageJpeg);

        // Decode the rasterized first page from the ZIP and sample the center of the box.
        using var archive = new ZipArchive(new MemoryStream(result.Document), ZipArchiveMode.Read);
        using var entryStream = archive.Entries[0].Open();
        using var pageImageBytes = new MemoryStream();
        entryStream.CopyTo(pageImageBytes);
        pageImageBytes.Position = 0;
        using var pageImage = SKBitmap.Decode(pageImageBytes);

        double pageWidth, pageHeight;
        using (var pdf = PdfDocument.Open(input))
        {
            var page = pdf.GetPage(1);
            pageWidth = page.Width;
            pageHeight = page.Height;
        }

        // Map the box center (PDF points, bottom-left origin) to a pixel (top-left origin).
        var scaleX = pageImage.Width / pageWidth;
        var scaleY = pageImage.Height / pageHeight;
        var centerX = (int)((100 + 200 / 2.0) * scaleX);
        var centerY = (int)((pageHeight - (300 + 200 / 2.0)) * scaleY);

        var pixel = pageImage.GetPixel(centerX, centerY);

        // The opaque red fill is drawn over the page content, so the box center is red
        // (JPEG is lossy, hence the tolerance).
        Assert.True(pixel.Red > 120, $"expected reddish pixel, got {pixel}");
        Assert.True(pixel.Green < 100, $"expected reddish pixel, got {pixel}");
        Assert.True(pixel.Blue < 100, $"expected reddish pixel, got {pixel}");
    }

    [Fact]
    public void ShowReplacement_RendersReplacementTextWithoutLeavingRecoverableText()
    {
        var policy = Policy();
        policy.Config.Pdf.ShowReplacement = true;

        var result = new PdfFilterService().Filter(policy, "ctx", SamplePdf(), MimeType.ApplicationPdf);

        // The replacement-text path runs for every span, and the output is still a valid,
        // fully-rasterized PDF with no recoverable text.
        Assert.NotEmpty(result.Spans);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(result.Document, 0, 4));
        using var document = PdfDocument.Open(result.Document);
        Assert.Empty(document.GetPages().SelectMany(p => p.Letters));
    }
}
