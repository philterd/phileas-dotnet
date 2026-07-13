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
}
