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

using System.Text.Json.Serialization;

namespace Phileas.Model;

/// <summary>
///     Represents a detected entity within a piece of text, including its character offsets,
///     filter type, replacement value, and associated metadata.
/// </summary>
public class Span
{
    /// <summary>Gets or sets the zero-based index of the first character of the detected entity.</summary>
    [JsonPropertyName("characterStart")]
    public int CharacterStart { get; set; }

    /// <summary>Gets or sets the zero-based exclusive index of the last character of the detected entity.</summary>
    [JsonPropertyName("characterEnd")]
    public int CharacterEnd { get; set; }

    /// <summary>Gets or sets the type of filter that produced this span.</summary>
    [JsonPropertyName("filterType")]
    public FilterType FilterType { get; set; }

    /// <summary>Gets or sets the context identifier associated with this span.</summary>
    [JsonPropertyName("context")]
    public string Context { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional classification label for the detected entity (e.g. "PERSON").</summary>
    [JsonPropertyName("classification")]
    public string? Classification { get; set; }

    /// <summary>Gets or sets the confidence score of the detection, in the range [0, 1].</summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>Gets or sets the original text of the detected entity.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the replacement value that will be substituted for the detected entity.</summary>
    [JsonPropertyName("replacement")]
    public string Replacement { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional cryptographic salt applied to the replacement.</summary>
    [JsonPropertyName("salt")]
    public string Salt { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether this entity was explicitly ignored by the policy.</summary>
    [JsonPropertyName("ignored")]
    public bool Ignored { get; set; }

    /// <summary>Gets or sets a value indicating whether the replacement strategy was applied.</summary>
    [JsonPropertyName("applied")]
    public bool Applied { get; set; }

    /// <summary>Gets or sets the priority of the filter that produced this span.</summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    /// <summary>Gets or sets the regex pattern name that matched this span. Not serialized.</summary>
    [JsonIgnore]
    public string? Pattern { get; set; }

    /// <summary>Gets or sets the surrounding words (context window) for the matched token. Not serialized.</summary>
    [JsonIgnore]
    public string[]? Window { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether this span is always treated as valid regardless of confidence. Not
    ///     serialized.
    /// </summary>
    [JsonIgnore]
    public bool AlwaysValid { get; set; }

    /// <summary>
    ///     Creates a new <see cref="Span" /> with the specified values.
    /// </summary>
    /// <param name="characterStart">Zero-based start index of the entity in the input text.</param>
    /// <param name="characterEnd">Zero-based exclusive end index of the entity in the input text.</param>
    /// <param name="filterType">The type of filter that detected this entity.</param>
    /// <param name="context">The context identifier.</param>
    /// <param name="confidence">Confidence score in the range [0, 1].</param>
    /// <param name="text">The original matched text.</param>
    /// <param name="replacement">The replacement value.</param>
    /// <param name="salt">Optional cryptographic salt.</param>
    /// <param name="ignored">Whether the entity was explicitly ignored.</param>
    /// <param name="applied">Whether the replacement was applied.</param>
    /// <param name="window">Surrounding context words.</param>
    /// <param name="priority">Priority of the producing filter.</param>
    /// <returns>A fully initialized <see cref="Span" />.</returns>
    public static Span Make(
        int characterStart,
        int characterEnd,
        FilterType filterType,
        string context,
        double confidence,
        string text,
        string replacement,
        string salt,
        bool ignored,
        bool applied,
        string[]? window,
        int priority)
    {
        return new Span
        {
            CharacterStart = characterStart,
            CharacterEnd = characterEnd,
            FilterType = filterType,
            Context = context,
            Confidence = confidence,
            Text = text,
            Replacement = replacement,
            Salt = salt,
            Ignored = ignored,
            Applied = applied,
            Window = window,
            Priority = priority
        };
    }

    /// <summary>
    ///     Determines whether a span with the given character offsets already exists in the provided list.
    /// </summary>
    /// <param name="characterStart">Start offset to check.</param>
    /// <param name="characterEnd">Exclusive end offset to check.</param>
    /// <param name="spans">The list of spans to search.</param>
    /// <returns><see langword="true" /> if a matching span already exists; otherwise <see langword="false" />.</returns>
    public static bool DoesSpanExist(int characterStart, int characterEnd, IList<Span> spans)
    {
        return spans.Any(s => s.CharacterStart == characterStart && s.CharacterEnd == characterEnd);
    }

    /// <summary>
    ///     Removes overlapping spans from the list, keeping the span with the highest confidence
    ///     when two spans cover overlapping character ranges.
    ///     The returned list is ordered by <see cref="CharacterStart" />.
    /// </summary>
    /// <param name="spans">The unfiltered collection of spans, potentially containing overlaps.</param>
    /// <returns>A new list of non-overlapping spans ordered by character position.</returns>
    public static IList<Span> DropOverlappingSpans(IList<Span> spans)
    {
        var sorted = spans.OrderByDescending(s => s.Confidence).ToList();
        var result = new List<Span>();

        foreach (var span in sorted)
        {
            var overlaps = result.Any(existing =>
                span.CharacterStart < existing.CharacterEnd &&
                span.CharacterEnd > existing.CharacterStart);

            if (!overlaps)
                result.Add(span);
        }

        return result.OrderBy(s => s.CharacterStart).ToList();
    }
}