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
using Phileas.Policy;
using Phileas.Services.Pdf;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace Phileas.Tests.Pdfs;

/// <summary>
///     With <c>preserveUnredactedPages</c>, a page with nothing to redact is copied into the output PDF unchanged and
///     every other page is rasterized. A page counts as having something to redact when a span or an enabled
///     bounding box is on it. See philterd/phileas-dotnet#146.
/// </summary>
[Collection("Pdf")]
public class PdfPreserveUnredactedPagesTests
{
    private const string Ssn = "Patient SSN 123-45-6789 on file.";
    private const string Clean = "Nothing sensitive here.";
    private const string Codename = "Project codename Bluebird is internal.";

    private static byte[] TextPdf(params string[] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var text in pages)
            builder.AddPage(612, 792).AddText(text, 12, new PdfPoint(72, 700), font);
        return builder.Build();
    }

    private static Policy.Policy PolicyJson(bool preserve, string boundingBoxes = "") =>
        PolicySerializer.DeserializeFromJson(
            "{\"config\":{\"pdf\":{\"preserveUnredactedPages\":" + preserve.ToString().ToLowerInvariant() + "}},"
            + "\"identifiers\":{\"ssn\":{}},\"graphical\":{\"boundingBoxes\":[" + boundingBoxes + "]}}");

    private static byte[] Redact(Policy.Policy policy, byte[] input, MimeType mime = MimeType.ApplicationPdf) =>
        new PdfFilterService().Filter(policy, "ctx", input, mime).Document;

    // Each output page's text layer, empty for a rasterized page.
    private static List<string> PageTexts(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return document.GetPages().Select(page => page.Text.Trim()).ToList();
    }

    [Fact]
    public void OptionOff_RasterizesEveryPage()
    {
        var output = Redact(PolicyJson(false), TextPdf(Ssn, Clean, Codename));

        Assert.Equal(new[] { "", "", "" }, PageTexts(output));
    }

    [Fact]
    public void OptionOn_CopiesThePagesWithNothingToRedact_InOrder()
    {
        var output = Redact(PolicyJson(true), TextPdf(Ssn, Clean, Codename));

        Assert.Equal(new[] { "", Clean, Codename }, PageTexts(output));
        using var document = PdfDocument.Open(output);
        Assert.Equal(612, document.GetPage(2).Width, 1);
        Assert.Equal(792, document.GetPage(2).Height, 1);
    }

    [Fact]
    public void OptionOn_APageCoveredOnlyByABoundingBox_IsRasterized()
    {
        // Copying this page would leave the text under the box in the output's text layer (philterd/phileas#420).
        var output = Redact(PolicyJson(true, "{\"page\":3,\"x\":60,\"y\":690,\"w\":300,\"h\":30}"),
            TextPdf(Ssn, Clean, Codename));

        Assert.Equal(new[] { "", Clean, "" }, PageTexts(output));
    }

    [Fact]
    public void OptionOn_ADisabledBoundingBox_DoesNotCountAsARedaction()
    {
        var output = Redact(PolicyJson(true, "{\"page\":3,\"x\":60,\"y\":690,\"w\":300,\"h\":30,\"enabled\":false}"),
            TextPdf(Ssn, Clean, Codename));

        Assert.Equal(new[] { "", Clean, Codename }, PageTexts(output));
    }

    [Fact]
    public void OptionOn_NothingToRedact_CopiesEveryPage()
    {
        var output = Redact(PolicyJson(true), TextPdf(Clean, Codename));

        Assert.Equal(new[] { Clean, Codename }, PageTexts(output));
    }

    [Fact]
    public void OptionOn_ImageOutput_StillRendersEveryPage()
    {
        var output = Redact(PolicyJson(true), TextPdf(Ssn, Clean, Codename), MimeType.ImageJpeg);

        using var archive = new ZipArchive(new MemoryStream(output));
        Assert.Equal(3, archive.Entries.Count);
    }

    [Fact]
    public void OptionOn_APageWhosePiiIsOnlyInAnAnnotation_IsRasterized()
    {
        // The annotation's text has no located box, but it is on page 2, so page 2 is not safe to copy.
        var input = RawPdf(new[] { "Clean body.", "Second body." },
            "<< /Type /Annot /Subtype /FreeText /Contents (SSN 123-45-6789) /Rect [72 650 300 670] /P 4 0 R >>",
            annotatedPage: 2, formFieldOnNoPage: false);

        var output = Redact(PolicyJson(true), input);

        var texts = PageTexts(output);
        Assert.Equal("Clean body.", texts[0]);
        Assert.Equal("", texts[1]);
    }

    [Fact]
    public void OptionOn_AFormFieldWithNoKnownPage_RasterizesEveryPage()
    {
        // The field's value is detected, but nothing ties it to a page, so no page is safe to copy.
        var input = RawPdf(new[] { "Clean body.", "Second body." }, annotation: null, annotatedPage: 0,
            formFieldOnNoPage: true);

        var output = Redact(PolicyJson(true), input);

        Assert.Equal(new[] { "", "" }, PageTexts(output));
    }

    [Fact]
    public void ASpanOnAPageOutsideTheDocument_LeavesNoPageToCopy()
    {
        var spans = new List<Span> { new() { PageNumber = 9 } };

        Assert.Equal(new[] { 1, 2, 3 }, PdfRedactor.PagesNeedingRedaction(spans, new List<BoundingBox>(), 3));
    }

    // A hand-authored PDF: one page per body text, an optional annotation on one page, and optionally an
    // AcroForm text field holding an SSN whose widget is on no page. ASCII-only, so byte offsets equal
    // character counts. Objects: 1 catalog, 2 pages, then a page and content stream per page, then the
    // annotation, the form field and the font.
    private static byte[] RawPdf(string[] bodies, string? annotation, int annotatedPage, bool formFieldOnNoPage)
    {
        var objects = new List<string>();
        var pageCount = bodies.Length;
        var firstPage = 3;
        var annotationObject = firstPage + pageCount * 2;
        var fieldObject = annotationObject + 1;
        var fontObject = fieldObject + 1;

        var acroForm = formFieldOnNoPage ? $" /AcroForm << /Fields [{fieldObject} 0 R] >>" : "";
        objects.Add($"<< /Type /Catalog /Pages 2 0 R{acroForm} >>");
        var kids = string.Join(" ", Enumerable.Range(0, pageCount).Select(i => $"{firstPage + i * 2} 0 R"));
        objects.Add($"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>");
        for (var i = 0; i < pageCount; i++)
        {
            var annots = annotation != null && annotatedPage == i + 1 ? $" /Annots [{annotationObject} 0 R]" : "";
            var content = $"BT /F1 12 Tf 72 700 Td ({bodies[i]}) Tj ET";
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents {firstPage + i * 2 + 1} 0 R "
                        + $"/Resources << /Font << /F1 {fontObject} 0 R >> >>{annots} >>");
            objects.Add($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
        }
        objects.Add(annotation ?? "<< >>");
        objects.Add("<< /FT /Tx /T (field1) /V (SSN 123-45-6789) >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        using var stream = new MemoryStream();
        void W(string s) => stream.Write(Encoding.Latin1.GetBytes(s));
        W("%PDF-1.7\n");
        var offsets = new long[objects.Count + 1];
        for (var i = 0; i < objects.Count; i++)
        {
            offsets[i + 1] = stream.Position;
            W($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = stream.Position;
        W($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        for (var i = 1; i <= objects.Count; i++)
            W($"{offsets[i]:D10} 00000 n \n");
        W($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return stream.ToArray();
    }
}
