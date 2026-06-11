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
using Xunit;

namespace Phileas.Tests;

public class PdfTests
{
    [Fact]
    public void CanSetPdfRedactorOptions()
    {
        var pdf = new Pdf
        {
            RedactionColor = "blue",
            ReplacementFont = "times",
            ShowReplacement = true,
            Scale = 2.0f,
            Dpi = 300,
            CompressionQuality = 0.5f,
            PreserveUnredactedPages = true
        };

        Assert.Equal("blue", pdf.RedactionColor);
        Assert.Equal("times", pdf.ReplacementFont);
        Assert.True(pdf.ShowReplacement);
        Assert.Equal(2.0f, pdf.Scale);
        Assert.Equal(300, pdf.Dpi);
        Assert.Equal(0.5f, pdf.CompressionQuality);
        Assert.True(pdf.PreserveUnredactedPages);
    }

    [Fact]
    public void DefaultsAreSetProperly()
    {
        var pdf = new Pdf();

        Assert.Equal("black", pdf.RedactionColor);
        Assert.Equal("helvetica", pdf.ReplacementFont);
        Assert.Equal(12, pdf.ReplacementMaxFontSize);
        Assert.Equal(0.25f, pdf.Scale);
        Assert.Equal(150, pdf.Dpi);
        Assert.Equal(1.0f, pdf.CompressionQuality);
        Assert.False(pdf.PreserveUnredactedPages);
    }
}
