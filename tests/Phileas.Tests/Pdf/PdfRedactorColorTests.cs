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

/// <summary>
///     Color resolution for PDF/image redaction bars: <see cref="PdfRedactor.ParseColor" /> maps the supported
///     color vocabulary, and end-to-end rendering honors a strategy's <c>color</c> over the policy-wide
///     <c>config.pdf.redactionColor</c>, falling back to black for an unrecognized value.
/// </summary>
[Collection("Pdf")]
public class PdfRedactorColorTests
{
    // ---- ParseColor: the supported color vocabulary ----

    [Theory]
    [InlineData("black", 0x00, 0x00, 0x00)]
    [InlineData("white", 0xFF, 0xFF, 0xFF)]
    [InlineData("red", 0xFF, 0x00, 0x00)]
    [InlineData("orange", 0xFF, 0xA5, 0x00)]
    [InlineData("yellow", 0xFF, 0xFF, 0x00)]
    [InlineData("green", 0x00, 0x80, 0x00)]
    [InlineData("blue", 0x00, 0x00, 0xFF)]
    [InlineData("gray", 0x80, 0x80, 0x80)]
    [InlineData("grey", 0x80, 0x80, 0x80)]
    [InlineData("  RED  ", 0xFF, 0x00, 0x00)] // trimmed and case-insensitive
    [InlineData("#ff8800", 0xFF, 0x88, 0x00)] // 6-digit hex
    [InlineData("#00AA55", 0x00, 0xAA, 0x55)] // hex is case-insensitive
    public void ParseColor_ResolvesTheSupportedVocabulary(string value, byte r, byte g, byte b)
    {
        var color = PdfRedactor.ParseColor(value, SKColors.Magenta);

        Assert.Equal(new SKColor(r, g, b), color);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("chartreuse")] // not a supported name
    [InlineData("#fff")] // 3-digit hex is not supported
    [InlineData("#gggggg")] // not hex digits
    [InlineData("#ff88000")] // too long
    [InlineData("ff8800")] // missing leading '#'
    public void ParseColor_FallsBackForUnrecognizedOrMalformedValues(string? value)
    {
        // The caller supplies black in production; a sentinel here proves the fallback path is taken.
        Assert.Equal(SKColors.Magenta, PdfRedactor.ParseColor(value, SKColors.Magenta));
    }

    // ---- End-to-end: the burned-in bar takes the resolved color ----

    private static byte[] SamplePdf() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Resources", "sample.pdf"));

    // Returns hand-built positioned lines regardless of the document (stands in for any text source).
    private sealed class StubTextExtractor : ITextExtractor
    {
        private readonly IReadOnlyList<PdfLine> _lines;
        public StubTextExtractor(IReadOnlyList<PdfLine> lines) => _lines = lines;
        public IReadOnlyList<PdfLine> GetLines(byte[] document) => _lines;
    }

    // Renders one SSN span with the given strategy/policy colors and samples the center of its bar.
    private static SKColor SampleSsnBar(string? strategyColor, string? redactionColor)
    {
        const string text = "SSN 123-45-6789";
        // One 10x60 pt box per character at y [100,160], so the sampled center sits well inside the bar.
        var boxes = new List<CharBox?>(text.Length);
        for (var i = 0; i < text.Length; i++)
            boxes.Add(new CharBox(i * 10, 100, i * 10 + 10, 160));
        var extractor = new StubTextExtractor(new[] { new PdfLine(1, text, boxes) });

        var policy = new PhileasPolicy
        {
            Name = "color",
            Identifiers = new PolicyIdentifiers
            {
                Ssn = new Ssn { Strategies = new List<SsnFilterStrategy> { new() { Color = strategyColor } } }
            }
        };
        if (redactionColor != null)
            policy.Config.Pdf.RedactionColor = redactionColor;

        var result = new PdfFilterService(new FilterService(), extractor)
            .Filter(policy, "ctx", SamplePdf(), MimeType.ImageJpeg);

        using var archive = new ZipArchive(new MemoryStream(result.Document), ZipArchiveMode.Read);
        using var entryStream = archive.Entries[0].Open();
        using var pageImageBytes = new MemoryStream();
        entryStream.CopyTo(pageImageBytes);
        pageImageBytes.Position = 0;
        using var pageImage = SKBitmap.Decode(pageImageBytes);

        double pageWidth, pageHeight;
        using (var pdf = PdfDocument.Open(SamplePdf()))
        {
            var page = pdf.GetPage(1);
            pageWidth = page.Width;
            pageHeight = page.Height;
        }

        // "123-45-6789" occupies x [40,150] pt; the box is y [100,160] pt. Sample its center, mapping from
        // PDF space (bottom-left origin) to image pixels (top-left origin).
        var scaleX = pageImage.Width / pageWidth;
        var scaleY = pageImage.Height / pageHeight;
        var px = (int)(95 * scaleX);
        var py = (int)((pageHeight - 130) * scaleY);
        return pageImage.GetPixel(px, py);
    }

    [Fact]
    public void StrategyColor_DrawsTheBarInThatColor()
    {
        var pixel = SampleSsnBar(strategyColor: "red", redactionColor: null);

        Assert.True(pixel.Red > 120 && pixel.Green < 100 && pixel.Blue < 100, $"expected red, got {pixel}");
    }

    [Fact]
    public void NoStrategyColor_FallsBackToTheConfiguredRedactionColor()
    {
        var pixel = SampleSsnBar(strategyColor: null, redactionColor: "blue");

        Assert.True(pixel.Blue > 120 && pixel.Red < 100 && pixel.Green < 100, $"expected blue, got {pixel}");
    }

    [Fact]
    public void StrategyColor_OverridesTheConfiguredRedactionColor()
    {
        var pixel = SampleSsnBar(strategyColor: "green", redactionColor: "red");

        Assert.True(pixel.Green > 80 && pixel.Red < 100 && pixel.Blue < 100, $"expected green, got {pixel}");
    }

    [Fact]
    public void HexStrategyColor_IsRendered()
    {
        var pixel = SampleSsnBar(strategyColor: "#ff8800", redactionColor: null);

        Assert.True(pixel.Red > 150 && pixel.Blue < 90, $"expected orange (#ff8800), got {pixel}");
    }

    [Fact]
    public void UnrecognizedStrategyColor_RendersBlack_NotTheConfiguredColor()
    {
        // A set-but-malformed strategy color renders black; it overrides the policy color, so it does not
        // fall through to the configured red. A detected value is never left un-redacted.
        var pixel = SampleSsnBar(strategyColor: "chartreuse", redactionColor: "red");

        Assert.True(pixel.Red < 80 && pixel.Green < 80 && pixel.Blue < 80, $"expected black, got {pixel}");
    }
}
