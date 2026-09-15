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
using Phileas.Services;
using Xunit;

namespace Phileas.Tests.Dictionaries;

/// <summary>
///     An n-gram's position is the position of the words it was built from. Searching the text for the
///     n-gram afterwards found the first place that text occurred, which redacted the wrong characters
///     when a term sat inside an earlier word, and collapsed a repeated term onto one position.
///     See philterd/phileas-dotnet#119.
/// </summary>
public class NgramPositionTests
{
    private static string Filtered(string json, string input)
    {
        return new FilterService().Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, input)
            .FilteredText;
    }

    private static IList<Model.Span> Spans(string json, string input)
    {
        return new FilterService().Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, input).Spans;
    }

    private static string Custom(string terms, bool fuzzy = false, string sensitivity = "off")
    {
        return "{\"identifiers\":{\"dictionaries\":[{\"terms\":[" + terms + "],\"fuzzy\":"
               + (fuzzy ? "true" : "false") + ",\"sensitivity\":\"" + sensitivity + "\"}]}}";
    }

    // ---------------- a term that also sits inside an earlier word ----------------

    [Theory]
    [InlineData("Smith", "Smithson Smith", "Smithson ")]
    [InlineData("Ann", "Anne Ann", "Anne ")]
    [InlineData("Li", "Lin Li", "Lin ")]
    [InlineData("b", "bb b", "bb ")]
    public void ATermInsideAnEarlierWordIsRedactedAtItsOwnPosition(string term, string input, string kept)
    {
        // The span used to land inside the earlier word, so a word that is not PII was corrupted and
        // the value that is PII stayed in the document. Both halves of that are asserted here.
        var filtered = Filtered(Custom("\"" + term + "\""), input);

        Assert.StartsWith(kept, filtered);
        Assert.DoesNotContain(kept + term, filtered);
        Assert.Contains("REDACTED", filtered);
    }

    [Fact]
    public void TheSpanTextIsWhatSitsAtTheSpansOffsets()
    {
        // The offsets and the reported text have to agree, which is what fails when a span is placed
        // on a different occurrence than the one it was built from.
        const string input = "Smithson Smith";
        var span = Assert.Single(Spans(Custom("\"Smith\""), input));

        Assert.Equal(span.Text, input.Substring(span.CharacterStart, span.CharacterEnd - span.CharacterStart));
        Assert.Equal(9, span.CharacterStart);
        Assert.Equal(14, span.CharacterEnd);
    }

    // ---------------- a repeated term ----------------

    [Theory]
    [InlineData("{\"identifiers\":{\"surname\":{}}}", "Smith Smith called", 2)]
    [InlineData("{\"identifiers\":{\"surname\":{}}}", "Smith Smith Smith", 3)]
    [InlineData("{\"identifiers\":{\"surname\":{}}}", "Smith, Smith", 2)]
    [InlineData("{\"identifiers\":{\"surname\":{}}}", "Smith and Smith", 2)]
    [InlineData("{\"identifiers\":{\"firstName\":{}}}", "John John called", 2)]
    [InlineData("{\"identifiers\":{\"city\":{}}}", "Boston Boston here", 2)]
    [InlineData("{\"identifiers\":{\"state\":{}}}", "Ohio Ohio here", 2)]
    public void ARepeatedTermIsDetectedAtEveryOccurrence(string json, string input, int expected)
    {
        Assert.Equal(expected, Spans(json, input).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)] // the fuzzy filter reported only the first exact match of each term
    public void ARepeatedTermIsDetectedOnBothTheExactAndFuzzyPaths(bool fuzzy)
    {
        var json = Custom("\"Acme\"", fuzzy, "medium");

        Assert.Equal(2, Spans(json, "Acme Acme here").Count);
        Assert.Equal(2, Spans(json, "Acme and Acme").Count);
        Assert.Equal(3, Spans(json, "Acme Acme Acme").Count);
    }

    [Fact]
    public void ARepeatedNearMatchIsDetectedAtEveryOccurrence()
    {
        // Smyth is one edit from Smith, so MEDIUM sensitivity takes it; both occurrences must land.
        Assert.Equal(2, Spans(Custom("\"Smith\"", fuzzy: true, sensitivity: "medium"), "Smyth Smyth").Count);
    }

    // ---------------- the cases the fix must not disturb ----------------

    [Theory]
    [InlineData("John Smith John Smith", 2)]
    [InlineData("see John Smith here", 1)]
    [InlineData("John Smith", 1)]
    [InlineData("John Smithson", 0)]
    public void MultiWordTermsStillMatchIncludingWhenRepeated(string input, int expected)
    {
        Assert.Equal(expected, Spans(Custom("\"John Smith\""), input).Count);
    }

    [Theory]
    [InlineData("Acme  Acme")] // two spaces
    [InlineData("Acme   x   Acme")]
    [InlineData(" Acme Acme ")]
    public void ARunOfSpacesDoesNotShiftAPosition(string input)
    {
        // Split(' ') does not coalesce a run of spaces, so the empty words between them still have to
        // account for every character or every later position drifts.
        var spans = Spans(Custom("\"Acme\""), input);

        Assert.Equal(2, spans.Count);
        Assert.All(spans, span => Assert.Equal("Acme",
            input.Substring(span.CharacterStart, span.CharacterEnd - span.CharacterStart)));
    }

    [Fact]
    public void NoTwoSpansCoverTheSameRange()
    {
        var spans = Spans(Custom("\"Acme\""), "Acme Acme Acme Acme");

        Assert.Equal(4, spans.Count);
        Assert.Equal(spans.Count, spans.Select(s => (s.CharacterStart, s.CharacterEnd)).Distinct().Count());
    }

    [Theory]
    [InlineData("off", "Smith", true)]
    [InlineData("off", "Smyth", false)]
    [InlineData("medium", "Smith", true)]
    [InlineData("medium", "Smyth", true)] // one edit away
    [InlineData("medium", "Jones", false)]
    public void TheFuzzySensitivityLevelsAreUnchanged(string sensitivity, string input, bool detected)
    {
        Assert.Equal(detected, Spans(Custom("\"Smith\"", fuzzy: true, sensitivity: sensitivity), input).Count > 0);
    }
}
