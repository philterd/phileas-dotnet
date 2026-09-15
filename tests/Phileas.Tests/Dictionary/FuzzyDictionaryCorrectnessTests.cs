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
///     Three defects in the fuzzy dictionary filter: a near match was dropped whenever the same term
///     also appeared exactly, the ignored check was given the whole document, and a span reported the
///     dictionary's spelling rather than the document's. See philterd/phileas-dotnet#126.
/// </summary>
public class FuzzyDictionaryCorrectnessTests
{
    private static string Policy(string strategy = "", bool fuzzy = true, string sensitivity = "medium",
        bool capitalized = false, string ignored = "")
    {
        return "{\"identifiers\":{\"dictionaries\":[{\"terms\":[\"Smith\"]"
               + (fuzzy ? ",\"fuzzy\":true,\"sensitivity\":\"" + sensitivity + "\"" : "")
               + (capitalized ? ",\"capitalized\":true" : "")
               + (ignored == "" ? "" : ",\"ignored\":[\"" + ignored + "\"]")
               + (strategy == "" ? "" : ",\"customFilterStrategies\":[{\"strategy\":\"" + strategy + "\"}]")
               + "}]}}";
    }

    private static Model.TextFilterResult Filter(string json, string input)
    {
        return new FilterService().Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, input);
    }

    // ---------------- an exact match no longer suppresses a near match ----------------

    [Theory]
    [InlineData("Smith Smyth", 2)]
    [InlineData("Smyth Smith", 2)]
    [InlineData("Smith Smyth Smith", 3)]
    [InlineData("Smyth Smyth", 2)]
    [InlineData("Smith Smith", 2)]
    public void ANearMatchIsDetectedEvenWhenTheTermAlsoAppearsExactly(string input, int expected)
    {
        // A name written correctly once and misspelled once is ordinary in a clinical record, and it
        // was the one case where fuzzy matching was asked for and did not happen.
        Assert.Equal(expected, Filter(Policy(), input).Spans.Count);
    }

    [Theory]
    [InlineData("Smith")]
    [InlineData("Smith Smith")]
    [InlineData("see Smith here")]
    public void AnExactOccurrenceIsReportedOnlyOnce(string input)
    {
        // An n-gram equal to the entry is also at distance 0, so running both scans could report the
        // same occurrence twice. This filter does not drop overlapping spans.
        var spans = Filter(Policy(), input).Spans;

        Assert.Equal(spans.Count, spans.Select(s => (s.CharacterStart, s.CharacterEnd)).Distinct().Count());
        Assert.All(spans, s => Assert.Equal("Smith", s.Text));
    }

    // ---------------- the span reports the document's text ----------------

    [Theory]
    [InlineData("smith here")]
    [InlineData("SMITH here")]
    [InlineData("Smith here")]
    public void ASpansTextIsWhatSitsAtItsOffsets(string input)
    {
        var span = Assert.Single(Filter(Policy(), input).Spans);

        Assert.Equal(span.Text, input.Substring(span.CharacterStart, span.CharacterEnd - span.CharacterStart));
    }

    [Theory]
    [InlineData("LAST_4", "SMITH")]
    [InlineData("LAST_4", "smith")]
    [InlineData("HASH_SHA256_REPLACE", "Smith")]
    [InlineData("HASH_SHA256_REPLACE", "smith")]
    [InlineData("REDACT", "smith")]
    public void TheTwoDictionaryFiltersAgreeOnWhatAStrategySees(string strategy, string input)
    {
        // The span's text is what a strategy operates on. Reporting the dictionary's spelling meant the
        // fuzzy filter hashed and truncated something other than what was in the document, and
        // disagreed with SetDictionaryFilter over the same input.
        Assert.Equal(
            Filter(Policy(strategy, fuzzy: false), input).FilteredText,
            Filter(Policy(strategy), input).FilteredText);
    }

    // ---------------- the ignored check sees the token ----------------

    [Fact]
    public void TheIgnoredCheckIsGivenTheMatchedValue()
    {
        // It was given the whole input, so it never fired and every span came back not ignored.
        var span = Assert.Single(Filter(Policy(ignored: "Smith"), "Smith here").Spans);

        Assert.True(span.Ignored);
    }

    [Fact]
    public void AValueThatIsNotIgnoredIsStillReportedAsNotIgnored()
    {
        var span = Assert.Single(Filter(Policy(ignored: "Jones"), "Smith here").Spans);

        Assert.False(span.Ignored);
    }

    // ---------------- what must not change ----------------

    [Theory]
    [InlineData("off", "Smith", true)]
    [InlineData("off", "Smiths", false)]
    [InlineData("high", "Smith", true)]
    [InlineData("high", "Smiths", false)]
    [InlineData("medium", "Smiths", true)] // one edit
    [InlineData("medium", "Smyths", false)] // two
    [InlineData("low", "Smyths", true)]
    public void TheSensitivityLevelsAreUnchanged(string sensitivity, string input, bool detected)
    {
        Assert.Equal(detected, Filter(Policy(sensitivity: sensitivity), input).Spans.Count > 0);
    }

    [Theory]
    [InlineData("Smith", true)]
    [InlineData("smith", false)]
    [InlineData("Smyth", true)]
    [InlineData("smyth", false)]
    public void CapitalizedIsStillRequiredWhereItIsSet(string input, bool detected)
    {
        Assert.Equal(detected, Filter(Policy(capitalized: true), input).Spans.Count > 0);
    }

    [Fact]
    public void ARepeatedTermIsStillDetectedAtEveryOccurrence()
    {
        // The #119 guarantee, which the exact scan carries.
        Assert.Equal(3, Filter(Policy(), "Smith Smith Smith").Spans.Count);
    }
}
