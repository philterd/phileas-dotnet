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
using Phileas.Policy;
using Phileas.Services;
using Phileas.Services.Pdf;
using SkiaSharp;
using UglyToad.PdfPig;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests.Pdfs;

/// <summary>
///     A graphical bounding box honours the shared filter properties the schema declares for it:
///     <c>enabled</c> decides whether it is drawn, and <c>priority</c> decides what it is drawn over.
///     Neither was read, so a box switched off still covered the page. See philterd/phileas-dotnet#85.
/// </summary>
[Collection("Pdf")]
public class PdfBoundingBoxRenderTests
{
    private static byte[] SamplePdf() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Resources", "sample.pdf"));

    /// <summary>Renders the page with the given boxes and samples the pixel at the centre of the first.</summary>
    private static SKColor SampleBoxPixel(params BoundingBox[] boxes)
    {
        var policy = new PhileasPolicy { Name = "boxes" };
        policy.Graphical.BoundingBoxes.AddRange(boxes);

        var result = new PdfFilterService(new FilterService())
            .Filter(policy, "ctx", SamplePdf(), MimeType.ImageJpeg);

        using var archive = new ZipArchive(new MemoryStream(result.Document), ZipArchiveMode.Read);
        using var entryStream = archive.Entries[0].Open();
        using var imageBytes = new MemoryStream();
        entryStream.CopyTo(imageBytes);
        imageBytes.Position = 0;
        using var pageImage = SKBitmap.Decode(imageBytes);

        double pageWidth, pageHeight;
        using (var pdf = PdfDocument.Open(SamplePdf()))
        {
            var page = pdf.GetPage(1);
            pageWidth = page.Width;
            pageHeight = page.Height;
        }

        // The centre of the first box, converted from PDF user space (bottom-left origin) to pixels.
        var box = boxes[0];
        var x = (int)((box.X + box.W / 2) * (pageImage.Width / pageWidth));
        var y = (int)((pageHeight - (box.Y + box.H / 2)) * (pageImage.Height / pageHeight));

        return pageImage.GetPixel(x, y);
    }

    private static BoundingBox Box(string color, bool enabled = true, int priority = 0) => new()
    {
        X = 50, Y = 400, W = 200, H = 100, Page = 1, Color = color, Enabled = enabled, Priority = priority
    };

    [Fact]
    public void AnEnabledBoxIsDrawn()
    {
        var pixel = SampleBoxPixel(Box("red"));

        Assert.True(pixel.Red > 120 && pixel.Green < 100 && pixel.Blue < 100, $"expected red, got {pixel}");
    }

    [Fact]
    public void ADisabledBoxIsNotDrawn()
    {
        // The page is white where the box would have been, so nothing was painted there.
        var pixel = SampleBoxPixel(Box("red", enabled: false));

        Assert.True(pixel.Red > 200 && pixel.Green > 200 && pixel.Blue > 200,
            $"expected the page to be left unpainted, got {pixel}");
    }

    [Fact]
    public void TheHigherPriorityBoxIsDrawnOnTop()
    {
        // Two boxes over the same area. Boxes are opaque, so the one drawn last is the one seen, and
        // priority decides which that is regardless of the order the policy declared them.
        var pixel = SampleBoxPixel(Box("blue", priority: 10), Box("red", priority: 1));

        Assert.True(pixel.Blue > 120 && pixel.Red < 100, $"expected blue to win on priority, got {pixel}");
    }

    [Fact]
    public void TheHigherPriorityBoxWinsWhicheverOrderTheyAreDeclared()
    {
        var pixel = SampleBoxPixel(Box("red", priority: 1), Box("blue", priority: 10));

        Assert.True(pixel.Blue > 120 && pixel.Red < 100, $"expected blue to win on priority, got {pixel}");
    }

    [Fact]
    public void WithEqualPrioritiesTheLastDeclaredWins()
    {
        // Stable ordering, so boxes sharing a priority keep the order the policy declared and the last
        // one is on top, which is what happened before priority was read at all.
        var pixel = SampleBoxPixel(Box("red"), Box("blue"));

        Assert.True(pixel.Blue > 120 && pixel.Red < 100, $"expected the last declared to win, got {pixel}");
    }
}
