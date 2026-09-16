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

using Phileas.Policy;
using Phileas.Services.Pdf;
using Xunit;

namespace Phileas.Tests.Pdfs;

/// <summary>
/// A graphical bounding box normally covers one exact page, but two page values are open-ended so a
/// single box can span pages without knowing the document length: <c>0</c> = every page, and <c>-N</c> =
/// page N through the last page (so <c>-2</c> is "all but the first page").
/// </summary>
public class PdfRedactorBoundingBoxTests
{
    private static bool Applies(int boxPage, int pageNumber) =>
        PdfRedactor.BoxAppliesToPage(new BoundingBox { Page = boxPage }, pageNumber);

    [Theory]
    [InlineData(3, 3, true)]   // exact page
    [InlineData(3, 2, false)]
    [InlineData(3, 4, false)]
    public void ExactPage_MatchesOnlyThatPage(int boxPage, int page, bool expected) =>
        Assert.Equal(expected, Applies(boxPage, page));

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(50, true)]
    public void PageZero_CoversEveryPage(int page, bool expected) =>
        Assert.Equal(expected, Applies(0, page));

    [Theory]
    [InlineData(1, false)]  // -2 = from page 2 on, so the first page is excluded
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(9, true)]
    public void NegativePage_CoversFromThatPageToTheEnd(int page, bool expected) =>
        Assert.Equal(expected, Applies(-2, page));

    // ---------------- the shared filter properties (#85) ----------------

    private static List<BoundingBox> Selected(params BoundingBox[] boxes) =>
        PdfRedactor.BoxesForPage(boxes, 1).ToList();

    [Fact]
    public void ADisabledBoxIsNotDrawn()
    {
        // enabled is declared by the schema for a bounding box and was not read, so a box switched off
        // still covered the page.
        var boxes = Selected(
            new BoundingBox { Page = 1, X = 1, Enabled = false },
            new BoundingBox { Page = 1, X = 2 });

        Assert.Equal(2, Assert.Single(boxes).X);
    }

    [Fact]
    public void EnabledDefaultsToDrawn()
    {
        Assert.Single(Selected(new BoundingBox { Page = 1 }));
    }

    [Fact]
    public void EveryBoxCanBeDisabled()
    {
        Assert.Empty(Selected(
            new BoundingBox { Page = 1, Enabled = false },
            new BoundingBox { Page = 0, Enabled = false }));
    }

    [Fact]
    public void ABoxIsDrawnAfterOneOfLowerPriority()
    {
        // Boxes are opaque, so the one drawn last is the one seen. Higher priority goes on top.
        var boxes = Selected(
            new BoundingBox { Page = 1, X = 1, Priority = 5 },
            new BoundingBox { Page = 1, X = 2, Priority = 10 },
            new BoundingBox { Page = 1, X = 3, Priority = 1 });

        Assert.Equal(new[] { 3f, 1f, 2f }, boxes.Select(b => b.X));
    }

    [Fact]
    public void BoxesSharingAPriorityKeepTheOrderThePolicyDeclared()
    {
        var boxes = Selected(
            new BoundingBox { Page = 1, X = 1 },
            new BoundingBox { Page = 1, X = 2 },
            new BoundingBox { Page = 1, X = 3 });

        Assert.Equal(new[] { 1f, 2f, 3f }, boxes.Select(b => b.X));
    }

    [Fact]
    public void PageSelectionStillApplies()
    {
        var boxes = PdfRedactor.BoxesForPage(new[]
        {
            new BoundingBox { Page = 1, X = 1 },
            new BoundingBox { Page = 2, X = 2 },
            new BoundingBox { Page = 0, X = 3 }
        }, 2).ToList();

        Assert.Equal(new[] { 2f, 3f }, boxes.Select(b => b.X).OrderBy(x => x));
    }

    [Fact]
    public void TheSharedFilterPropertiesBindOnABoundingBox()
    {
        // The schema inlines six of the abstract filter properties onto a bounding box. It does not
        // declare id there, so that one is deliberately absent.
        const string json = "{\"graphical\":{\"boundingBoxes\":[{\"x\":1,\"y\":2,\"w\":3,\"h\":4,"
                            + "\"enabled\":false,\"priority\":7,\"windowSize\":9,\"ignored\":[\"a\"],"
                            + "\"ignoredFiles\":[\"f\"],"
                            + "\"ignoredPatterns\":[{\"name\":\"n\",\"pattern\":\"^x$\"}]}]}}";
        var box = Assert.Single(PolicySerializer.DeserializeFromJson(json).Graphical.BoundingBoxes);

        Assert.False(box.Enabled);
        Assert.Equal(7, box.Priority);
        Assert.Equal(9, box.WindowSize);
        Assert.Single(box.Ignored!);
        Assert.Single(box.IgnoredFiles!);
        Assert.Single(box.IgnoredPatterns!);
    }
}
