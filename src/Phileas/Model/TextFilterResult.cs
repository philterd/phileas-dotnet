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
/// The result produced by <see cref="IFilterService.Filter"/> containing the redacted text
/// and the list of <see cref="Span"/> objects that describe each detected entity.
/// </summary>
public class TextFilterResult
{
    /// <summary>Gets the input text with all detected entities replaced by their configured redaction values.</summary>
    public string FilteredText { get; }

    /// <summary>Gets the ordered list of spans that were identified and replaced.</summary>
    public IList<Span> Spans { get; }

    /// <summary>
    /// Initializes a new <see cref="TextFilterResult"/>.
    /// </summary>
    /// <param name="filteredText">The redacted output text.</param>
    /// <param name="spans">The spans that were identified in the input.</param>
    public TextFilterResult(string filteredText, IList<Span> spans)
    {
        FilteredText = filteredText;
        Spans = spans;
    }
}
