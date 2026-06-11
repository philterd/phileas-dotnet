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

    /// <summary>Gets or sets the 1-based page number this span was found on (PDF documents). Not serialized.</summary>
    [JsonIgnore]
    public int PageNumber { get; set; }

    /// <summary>Gets or sets the lower-left X coordinate of the span's bounding box in PDF user space. Not serialized.</summary>
    [JsonIgnore]
    public double LowerLeftX { get; set; }

    /// <summary>Gets or sets the lower-left Y coordinate of the span's bounding box in PDF user space. Not serialized.</summary>
    [JsonIgnore]
    public double LowerLeftY { get; set; }

    /// <summary>Gets or sets the upper-right X coordinate of the span's bounding box in PDF user space. Not serialized.</summary>
    [JsonIgnore]
    public double UpperRightX { get; set; }

    /// <summary>Gets or sets the upper-right Y coordinate of the span's bounding box in PDF user space. Not serialized.</summary>
    [JsonIgnore]
    public double UpperRightY { get; set; }

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

    /// <summary>Returns a deep copy of this span.</summary>
    public Span Copy()
    {
        return new Span
        {
            CharacterStart = CharacterStart,
            CharacterEnd = CharacterEnd,
            FilterType = FilterType,
            Context = Context,
            Classification = Classification,
            Confidence = Confidence,
            Text = Text,
            Replacement = Replacement,
            Salt = Salt,
            Ignored = Ignored,
            Applied = Applied,
            Priority = Priority,
            Pattern = Pattern,
            Window = Window,
            AlwaysValid = AlwaysValid,
            PageNumber = PageNumber,
            LowerLeftX = LowerLeftX,
            LowerLeftY = LowerLeftY,
            UpperRightX = UpperRightX,
            UpperRightY = UpperRightY
        };
    }

    /// <summary>Returns copies of <paramref name="spans" /> with their character offsets shifted by <paramref name="offset" />.</summary>
    public static IList<Span> ShiftSpans(int offset, IEnumerable<Span> spans)
    {
        var shifted = new List<Span>();
        foreach (var span in spans)
        {
            var copy = span.Copy();
            copy.CharacterStart += offset;
            copy.CharacterEnd += offset;
            shifted.Add(copy);
        }

        return shifted;
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
        // Rank by length (longest first), then confidence, then priority, then start position, and
        // greedily keep each span that does not overlap an already-kept span. The overlap check treats
        // the character range as inclusive at both ends, mirroring the Java reference.
        var ranked = spans
            .OrderByDescending(s => s.CharacterEnd - s.CharacterStart)
            .ThenByDescending(s => s.Confidence)
            .ThenByDescending(s => s.Priority)
            .ThenBy(s => s.CharacterStart)
            .ToList();

        var result = new List<Span>();

        foreach (var span in ranked)
        {
            var overlaps = result.Any(existing =>
                span.CharacterStart <= existing.CharacterEnd &&
                existing.CharacterStart <= span.CharacterEnd);

            if (!overlaps)
                result.Add(span);
        }

        return result.OrderBy(s => s.CharacterStart).ToList();
    }

    /// <summary>
    ///     Determines whether <paramref name="span2" /> directly follows <paramref name="span1" /> in
    ///     <paramref name="text" />, separated only by whitespace or a comma.
    /// </summary>
    public static bool AreSpansAdjacent(Span span1, Span span2, string text)
    {
        if (span1.CharacterStart > span2.CharacterStart) return false;

        if (span1.CharacterEnd == span1.CharacterStart + 1) return true;

        var separators = text[span1.CharacterEnd..(span2.CharacterStart - 1)];
        return separators.All(char.IsWhiteSpace) || separators.Trim() == ",";
    }

    /// <summary>
    ///     Returns the spans in <paramref name="spans" /> that cover the same range as
    ///     <paramref name="span" /> with the same confidence but a different filter type.
    /// </summary>
    public static IList<Span> GetIdenticalSpans(Span span, IList<Span> spans)
    {
        return spans.Where(candidate =>
                candidate.CharacterStart == span.CharacterStart
                && candidate.CharacterEnd == span.CharacterEnd
                && candidate.FilterType != span.FilterType
                && Math.Abs(candidate.Confidence - span.Confidence) == 0)
            .ToList();
    }

    /// <summary>Returns the span that starts at <paramref name="index" />, or <see langword="null" />.</summary>
    public static Span? DoesIndexStartSpan(int index, IList<Span> spans)
    {
        return spans.FirstOrDefault(span => span.CharacterStart == index);
    }

    /// <summary>
    ///     Returns copies of every span except <paramref name="ignoreSpan" /> with their character
    ///     offsets shifted by <paramref name="offset" />.
    /// </summary>
    public static IList<Span> ShiftSpans(int offset, Span ignoreSpan, IEnumerable<Span> spans)
    {
        return ShiftSpans(offset, spans.Where(span => !ReferenceEquals(span, ignoreSpan)));
    }
}