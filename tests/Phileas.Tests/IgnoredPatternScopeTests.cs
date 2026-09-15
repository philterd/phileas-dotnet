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

namespace Phileas.Tests;

/// <summary>
///     An ignored pattern suppresses a detection wherever the policy declares it. Two of the three
///     placements used to do nothing: the top-level list was never applied, and the dictionary filters
///     never ran their post-filters. See philterd/phileas-dotnet#124.
/// </summary>
public class IgnoredPatternScopeTests
{
    private static Model.TextFilterResult Filter(string json, string input)
    {
        return new FilterService().Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, input);
    }

    // ---------------- every placement, against every kind of filter ----------------

    [Theory]
    // Top level, which was never applied at all.
    [InlineData("{\"identifiers\":{\"ssn\":{}},"
                + "\"ignoredPatterns\":[{\"name\":\"n\",\"pattern\":\"^078\"}]}", "078-05-1120")]
    [InlineData("{\"identifiers\":{\"dictionaries\":[{\"terms\":[\"Acme\"]}]},"
                + "\"ignoredPatterns\":[{\"name\":\"n\",\"pattern\":\"^Acme$\"}]}", "Acme")]
    [InlineData("{\"identifiers\":{\"surname\":{}},"
                + "\"ignoredPatterns\":[{\"name\":\"n\",\"pattern\":\"^Smith$\"}]}", "Smith")]
    // Per filter, which worked only on the regex filters.
    [InlineData("{\"identifiers\":{\"ssn\":{\"ignoredPatterns\":[{\"name\":\"n\",\"pattern\":\"^078\"}]}}}",
        "078-05-1120")]
    [InlineData("{\"identifiers\":{\"dictionaries\":[{\"terms\":[\"Acme\"],"
                + "\"ignoredPatterns\":[{\"name\":\"n\",\"pattern\":\"^Acme$\"}]}]}}", "Acme")]
    [InlineData("{\"identifiers\":{\"surname\":{\"ignoredPatterns\":[{\"name\":\"n\",\"pattern\":\"^Smith$\"}]}}}",
        "Smith")]
    public void AnIgnoredPatternSuppressesTheDetection(string json, string input)
    {
        Assert.Equal(input, Filter(json, input).FilteredText);
        Assert.Empty(Filter(json, input).Spans);
    }

    [Theory]
    [InlineData("{\"identifiers\":{\"dictionaries\":[{\"terms\":[\"Acme\"],\"ignored\":[\"Acme\"]}]}}", "Acme")]
    [InlineData("{\"identifiers\":{\"surname\":{\"fuzzy\":true,\"ignored\":[\"Smith\"]}}}", "Smith")]
    public void APerFilterIgnoredTermAlsoReachesADictionaryFilter(string json, string input)
    {
        // Same cause: the dictionary filters returned their spans without running PostFilter, which is
        // where ignored terms are applied as well as ignored patterns.
        Assert.Equal(input, Filter(json, input).FilteredText);
    }

    [Fact]
    public void APatternThatDoesNotMatchLeavesTheDetectionAlone()
    {
        const string json = "{\"identifiers\":{\"dictionaries\":[{\"terms\":[\"Acme\"]}]},"
                            + "\"ignoredPatterns\":[{\"name\":\"n\",\"pattern\":\"^Nope$\"}]}";

        Assert.NotEqual("Acme", Filter(json, "Acme").FilteredText);
    }

    [Fact]
    public void TheTopLevelListAppliesToEveryFilterAtOnce()
    {
        // The point of the top-level list: one pattern, every identifier type.
        const string json = "{\"identifiers\":{\"ssn\":{},\"emailAddress\":{},\"dictionaries\":[{\"terms\":[\"Acme\"]}]},"
                            + "\"ignoredPatterns\":[{\"name\":\"n\",\"pattern\":\"^(078-05-1120|a@b.com|Acme)$\"}]}";
        const string input = "078-05-1120 a@b.com Acme";

        Assert.Equal(input, Filter(json, input).FilteredText);
    }

    [Theory]
    [InlineData("^ACME$", false)] // matching is case-sensitive, as #88 settled
    [InlineData("(?i)^ACME$", true)]
    [InlineData("^Acme$", true)]
    public void TheTopLevelListMatchesCaseSensitively(string pattern, bool suppressed)
    {
        var json = "{\"identifiers\":{\"dictionaries\":[{\"terms\":[\"Acme\"]}]},"
                   + "\"ignoredPatterns\":[{\"name\":\"n\",\"pattern\":\"" + pattern.Replace("\\", "\\\\") + "\"}]}";

        Assert.Equal(suppressed, Filter(json, "Acme").FilteredText == "Acme");
    }

    // ---------------- a pattern that cannot be evaluated ----------------

    [Fact]
    public void ATopLevelPatternThatTimesOutKeepsTheDetection()
    {
        // Failing closed: an ignored pattern that cannot be evaluated must not drop a detection, which
        // would leave the value in the clear. The per-filter path already behaves this way.
        //
        // The span's text has to make the pattern *fail*, since a match returns immediately; it is the
        // failing case that backtracks catastrophically. So the detected value ends in a character the
        // pattern cannot accept.
        const string catastrophic = "^(a+)+$";
        var detected = new string('a', 40) + "!";
        var json = "{\"identifiers\":{\"identifiers\":[{\"classification\":\"c\",\"pattern\":\"a+!\"}]},"
                   + "\"ignoredPatterns\":[{\"name\":\"n\",\"pattern\":\"" + catastrophic + "\"}]}";

        var result = Filter(json, detected);

        Assert.NotEmpty(result.Spans);
        Assert.Contains(result.RegexTimeouts, t => t.Contains(catastrophic));
    }

    // ---------------- the post-filters the dictionary filters now run ----------------

    [Theory]
    [InlineData("Acme.", 0, 4)]
    [InlineData("Acme ", 0, 4)]
    [InlineData("see Acme. end", 4, 8)]
    public void RunningThePostFiltersDoesNotMoveADictionarySpan(string input, int start, int end)
    {
        // The dictionary filters already trimmed punctuation themselves, so applying the trailing
        // period and space post-filters leaves their boundaries where they were. Pinned so the change
        // stays boundary-neutral.
        var span = Assert.Single(Filter("{\"identifiers\":{\"dictionaries\":[{\"terms\":[\"Acme\"]}]}}", input).Spans);

        Assert.Equal(start, span.CharacterStart);
        Assert.Equal(end, span.CharacterEnd);
        Assert.Equal("Acme", span.Text);
    }

    [Fact]
    public void AMultiWordTermKeepsItsBoundaries()
    {
        var span = Assert.Single(
            Filter("{\"identifiers\":{\"dictionaries\":[{\"terms\":[\"John Smith\"]}]}}", "see John Smith. end").Spans);

        Assert.Equal("John Smith", span.Text);
        Assert.Equal(4, span.CharacterStart);
        Assert.Equal(14, span.CharacterEnd);
    }
}
