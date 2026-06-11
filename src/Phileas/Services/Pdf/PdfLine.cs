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

using UglyToad.PdfPig.Content;

namespace Phileas.Services.Pdf;

/// <summary>
///     A single line of text extracted from a PDF page, together with the source glyph for each
///     character so a detected span can be mapped back to coordinates on the page.
/// </summary>
public sealed class PdfLine
{
    /// <summary>Creates a PDF line.</summary>
    /// <param name="pageNumber">The 1-based page number this line is on.</param>
    /// <param name="text">The reconstructed line text.</param>
    /// <param name="lettersByChar">
    ///     The source glyph for each character in <paramref name="text" /> (same length); entries are
    ///     <see langword="null" /> for synthesized separators such as the spaces inserted between words.
    /// </param>
    public PdfLine(int pageNumber, string text, IReadOnlyList<Letter?> lettersByChar)
    {
        PageNumber = pageNumber;
        Text = text;
        LettersByChar = lettersByChar;
    }

    /// <summary>Gets the 1-based page number this line is on.</summary>
    public int PageNumber { get; }

    /// <summary>Gets the reconstructed line text.</summary>
    public string Text { get; }

    /// <summary>Gets the source glyph for each character in <see cref="Text" /> (<see langword="null" /> for separators).</summary>
    public IReadOnlyList<Letter?> LettersByChar { get; }
}
