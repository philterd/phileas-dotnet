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

namespace Phileas.Services.Pdf;

/// <summary>
///     The bounding box of a single character on a page, in PDF user-space points (1/72 inch) with a
///     bottom-left origin. Kept independent of any particular extraction library so lines can be produced
///     from a PDF text layer, from OCR, or from any other source.
/// </summary>
public readonly struct CharBox
{
    /// <summary>Creates a character bounding box (PDF user-space points, bottom-left origin).</summary>
    public CharBox(double left, double bottom, double right, double top)
    {
        Left = left;
        Bottom = bottom;
        Right = right;
        Top = top;
    }

    /// <summary>Gets the left edge (points).</summary>
    public double Left { get; }

    /// <summary>Gets the bottom edge (points).</summary>
    public double Bottom { get; }

    /// <summary>Gets the right edge (points).</summary>
    public double Right { get; }

    /// <summary>Gets the top edge (points).</summary>
    public double Top { get; }
}

/// <summary>
///     A single line of text extracted from a PDF page, together with the bounding box of each character
///     so a detected span can be mapped back to coordinates on the page. The boxes are source-agnostic
///     (PDF text layer, OCR, etc.).
/// </summary>
public sealed class PdfLine
{
    /// <summary>Creates a PDF line.</summary>
    /// <param name="pageNumber">The 1-based page number this line is on.</param>
    /// <param name="text">The reconstructed line text.</param>
    /// <param name="charBoxes">
    ///     The bounding box for each character in <paramref name="text" /> (same length); entries are
    ///     <see langword="null" /> for synthesized separators such as the spaces inserted between words.
    /// </param>
    public PdfLine(int pageNumber, string text, IReadOnlyList<CharBox?> charBoxes)
    {
        PageNumber = pageNumber;
        Text = text;
        CharBoxes = charBoxes;
    }

    /// <summary>Gets the 1-based page number this line is on.</summary>
    public int PageNumber { get; }

    /// <summary>Gets the reconstructed line text.</summary>
    public string Text { get; }

    /// <summary>Gets the bounding box for each character in <see cref="Text" /> (<see langword="null" /> for separators).</summary>
    public IReadOnlyList<CharBox?> CharBoxes { get; }
}
