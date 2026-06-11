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

using System.Text;

namespace Phileas.Model;

/// <summary>
///     The result produced by <see cref="Phileas.Services.IFilterService" /> containing the redacted text,
///     the list of <see cref="Span" /> objects that describe each detected entity, and (optionally) the
///     incremental redaction trail and token count.
/// </summary>
public class TextFilterResult
{
    /// <summary>
    ///     Initializes a new <see cref="TextFilterResult" />.
    /// </summary>
    /// <param name="filteredText">The redacted output text.</param>
    /// <param name="spans">The spans that were identified in the input.</param>
    public TextFilterResult(string filteredText, IList<Span> spans)
        : this(filteredText, string.Empty, 0, spans, new List<IncrementalRedaction>(), 0)
    {
    }

    /// <summary>Initializes a fully populated <see cref="TextFilterResult" />.</summary>
    /// <param name="filteredText">The redacted output text.</param>
    /// <param name="context">The context identifier.</param>
    /// <param name="piece">Zero-based piece index within a multi-part document.</param>
    /// <param name="spans">The spans that were identified in the input.</param>
    /// <param name="incrementalRedactions">The per-redaction snapshot trail (empty when disabled).</param>
    /// <param name="tokens">The number of tokens in the input.</param>
    public TextFilterResult(string filteredText, string context, int piece, IList<Span> spans,
        IList<IncrementalRedaction> incrementalRedactions, long tokens)
    {
        FilteredText = filteredText;
        Context = context;
        Piece = piece;
        Spans = spans;
        IncrementalRedactions = incrementalRedactions;
        Tokens = tokens;
    }

    /// <summary>Gets the input text with all detected entities replaced by their configured redaction values.</summary>
    public string FilteredText { get; }

    /// <summary>Gets the context identifier.</summary>
    public string Context { get; }

    /// <summary>Gets the zero-based piece index within a multi-part document.</summary>
    public int Piece { get; }

    /// <summary>Gets the ordered list of spans that were identified and replaced.</summary>
    public IList<Span> Spans { get; }

    /// <summary>Gets the per-redaction snapshot trail (empty when incremental redactions are disabled).</summary>
    public IList<IncrementalRedaction> IncrementalRedactions { get; }

    /// <summary>Gets the number of tokens in the input.</summary>
    public long Tokens { get; }

    /// <summary>
    ///     Combines per-piece results (in piece order) into a single result: the filtered texts are
    ///     joined with <paramref name="separator" /> and the span offsets are shifted to their positions
    ///     in the combined document.
    /// </summary>
    public static TextFilterResult Combine(IList<TextFilterResult> results, string context, string separator)
    {
        var filteredText = new StringBuilder();
        var spans = new List<Span>();
        var incrementalRedactions = new List<IncrementalRedaction>();
        long tokens = 0;
        var documentOffset = 0;

        foreach (var result in results.OrderBy(r => r.Piece))
        {
            var pieceFilteredText = result.FilteredText + separator;
            filteredText.Append(pieceFilteredText);

            spans.AddRange(Span.ShiftSpans(documentOffset, result.Spans));
            documentOffset += pieceFilteredText.Length;

            incrementalRedactions.AddRange(result.IncrementalRedactions);
            tokens += result.Tokens;
        }

        return new TextFilterResult(filteredText.ToString().Trim(), context, 0, spans, incrementalRedactions,
            tokens);
    }
}
