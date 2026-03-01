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

namespace Phileas.Model;

/// <summary>
/// Holds the result of a single filter pass against a piece of text, including the context
/// identifier, piece index, and the list of detected <see cref="Span"/> objects.
/// </summary>
public class Filtered
{
    /// <summary>Gets the context identifier associated with this filtering result.</summary>
    public string Context { get; }

    /// <summary>Gets the zero-based piece index within a multi-part document.</summary>
    public int Piece { get; }

    /// <summary>Gets the list of spans detected by the filter.</summary>
    public IList<Span> Spans { get; }

    /// <summary>
    /// Initializes a new <see cref="Filtered"/> for piece 0.
    /// </summary>
    /// <param name="context">The context identifier.</param>
    /// <param name="spans">The detected spans.</param>
    public Filtered(string context, IList<Span> spans)
    {
        Context = context;
        Piece = 0;
        Spans = spans;
    }

    /// <summary>
    /// Initializes a new <see cref="Filtered"/> with an explicit piece index.
    /// </summary>
    /// <param name="context">The context identifier.</param>
    /// <param name="piece">Zero-based piece index within a multi-part document.</param>
    /// <param name="spans">The detected spans.</param>
    public Filtered(string context, int piece, IList<Span> spans)
    {
        Context = context;
        Piece = piece;
        Spans = spans;
    }
}
