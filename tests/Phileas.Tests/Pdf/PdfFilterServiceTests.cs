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

    private static int PageCount(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return document.NumberOfPages;
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
