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

namespace Phileas.Services.Office;

/// <summary>
///     A single redaction captured or applied by the Office (<c>.docx</c>/<c>.xlsx</c>) redactors. It
///     carries the location — a <see cref="ParagraphIndex"/> (the docx paragraph / xlsx cell ordinal)
///     plus character offsets, or <see cref="ParagraphIndex"/> <c>-1</c> for content that isn't a body
///     paragraph/cell (a drawing, chart, comment, header/footer, hyperlink target, field instruction, or
///     embedded object) — the original and replacement text, a classification, and the engine's
///     "why was this flagged" explanation detail.
///
///     This is the library-side, persistence-free currency of the Office redactors (the analog of the
///     PDF path's <see cref="Phileas.Model.Span"/>). A consuming application maps it to its own storage
///     type; the library itself never persists it.
/// </summary>
public sealed class OfficeRedactionSpan
{
    /// <summary>Stable display/apply order within a single redaction pass.</summary>
    public int Order { get; set; }

    /// <summary>
    ///     Index into the document's canonical paragraph (docx) or cell (xlsx) enumeration, used to
    ///     re-apply the span by position; <c>-1</c> for non-paragraph/cell content.
    /// </summary>
    public int ParagraphIndex { get; set; } = -1;

    /// <summary>Zero-based start offset of the redacted range within its paragraph/cell text.</summary>
    public int CharacterStart { get; set; }

    /// <summary>Zero-based exclusive end offset of the redacted range within its paragraph/cell text.</summary>
    public int CharacterEnd { get; set; }

    /// <summary>The original (detected) text. For a user-added span this is the term to redact.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The replacement text written in place of <see cref="Text"/>.</summary>
    public string Replacement { get; set; } = string.Empty;

    /// <summary>Filter type / label (e.g. "email-address", "ssn"), informational.</summary>
    public string Classification { get; set; } = string.Empty;

    // --- Explanation detail (copied from the engine Span at capture time) ---------------------------

    /// <summary>The engine filter that matched (e.g. "EMAIL_ADDRESS", "SSN").</summary>
    public string FilterType { get; set; } = string.Empty;

    /// <summary>The engine's confidence in this detection (0–1).</summary>
    public double Confidence { get; set; }

    /// <summary>The rule/regex pattern that matched, when the filter is pattern-based.</summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>The surrounding tokens (context window) the engine considered, when available.</summary>
    public List<string> Window { get; set; } = new();
}
