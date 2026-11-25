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

using Phileas.Model;
using Phileas.Policy.Filters;
using Phileas.Services.Pdf;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;
using PolicyIdentifiers = Phileas.Policy.Identifiers;

namespace Phileas.Tests.Pdfs;

/// <summary>
///     An SSN written one digit to a box, as in the comb fields on Form 1040, is redacted. Each digit is
///     extracted as a word of its own, so the line reads <c>5 1 2 4 8 7 3 2 9</c>.
/// </summary>
[Collection("Pdf")]
public class PdfCombFieldTests
{
    private const double FirstBox = 400;
    private const double BoxWidth = 14.4;

    /// <summary>A page with Form 1040's SSN label and an SSN drawn one digit to a 14.4 pt box below it.</summary>
    private static byte[] CombFieldPdf(string digits)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(612, 792);
        page.AddText("Your social security number", 8, new PdfPoint(FirstBox, 712), font);
        for (var i = 0; i < digits.Length; i++)
            page.AddText(digits[i].ToString(), 10, new PdfPoint(FirstBox + 4 + i * BoxWidth, 700), font);
        return builder.Build();
    }

    private static PhileasPolicy Policy() => new()
    {
        Name = "1040",
        Identifiers = new PolicyIdentifiers { Ssn = new Ssn(), Itin = new Itin() }
    };

    [Theory]
    [InlineData("512487329", FilterType.Ssn)]
    [InlineData("912701234", FilterType.Itin)]
    public void AnIdentifierOneDigitToABoxIsRedactedAcrossItsBoxes(string digits, FilterType type)
    {
        var pdf = CombFieldPdf(digits);
        var spaced = string.Join(' ', digits.ToCharArray());
        Assert.Contains(new PdfTextExtractor().GetLines(pdf), line => line.Text == spaced);

        var result = new PdfFilterService().Filter(Policy(), "default", pdf, MimeType.ApplicationPdf);

        var span = Assert.Single(result.Spans);
        Assert.Equal(type, span.FilterType);
        Assert.Equal(spaced, span.Text);
        Assert.InRange(span.LowerLeftX, FirstBox, FirstBox + BoxWidth);
        Assert.InRange(span.UpperRightX, FirstBox + 8 * BoxWidth, FirstBox + 9 * BoxWidth);
        Assert.True(span.UpperRightY > span.LowerLeftY);
    }
}
