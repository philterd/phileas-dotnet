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

using Phileas.Filters.Rules.Regex.RegexFilters;
using Phileas.Filters.Strategies.Rules;
using Phileas.Model;
using Phileas.Policy.Filters;
using Xunit;
using static Phileas.Tests.IdentifierSectionTestSupport;

namespace Phileas.Tests;

public class IdentifierFilterTests
{
    private static IdentifierFilter Default() =>
        new(Config(new IdentifierFilterStrategy()), "name", Identifier.DefaultIdentifierRegex, true, 0);

    private static IdentifierFilter Custom(string regex, bool caseSensitive) =>
        new(Config(new IdentifierFilterStrategy()), "name", regex, caseSensitive, 0);

    [Fact]
    public void Id1()
    {
        var filtered = Default().Filter(GetPolicy(), "context", Piece, "the id is AB4736021 in california.");
        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 10, 19, FilterType.Identifier));
        Assert.Equal("AB4736021", filtered.Spans[0].Text);
    }

    [Fact]
    public void Id2()
    {
        var filtered = Default().Filter(GetPolicy(), "context", Piece, "the id is AB473-6021 in california.");
        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 10, 20, FilterType.Identifier));
    }

    [Fact]
    public void Id3()
    {
        var filtered = Default().Filter(GetPolicy(), "context", Piece, "the id is 473-6AB021 in california.");
        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 10, 20, FilterType.Identifier));
    }

    [Fact]
    public void Id6()
    {
        var filtered = Default().Filter(GetPolicy(), "context", Piece, "the id is 123-45-6789 in california.");
        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 10, 21, FilterType.Identifier));
    }

    [Fact]
    public void Id7()
    {
        var filtered = Default().Filter(GetPolicy(), "context", Piece,
            "George Washington was president and his ssn was 123-45-6789 and he lived at 90210. "
            + "Patient id 00076A and 93821A. He is on biotin. Diagnosed with A01000.");
        Assert.Equal(4, filtered.Spans.Count);
        Assert.True(CheckSpan(filtered.Spans[0], 48, 59, FilterType.Identifier));
        Assert.True(CheckSpan(filtered.Spans[1], 94, 100, FilterType.Identifier));
        Assert.True(CheckSpan(filtered.Spans[2], 105, 111, FilterType.Identifier));
        Assert.True(CheckSpan(filtered.Spans[3], 145, 151, FilterType.Identifier));
    }

    [Fact]
    public void Id8()
    {
        var filtered = Default().Filter(GetPolicy(), "context", Piece, "the id is 000-00-00-00 ABC123 in california.");
        Assert.Equal(2, filtered.Spans.Count);
        Assert.True(CheckSpan(filtered.Spans[0], 10, 22, FilterType.Identifier));
        Assert.True(CheckSpan(filtered.Spans[1], 23, 29, FilterType.Identifier));
    }

    [Fact]
    public void Id9()
    {
        var filtered = Default().Filter(GetPolicy(), "context", Piece, "the id is AZ12 ABC1234/123ABC4 in california.");
        Assert.Equal(2, filtered.Spans.Count);
        Assert.True(CheckSpan(filtered.Spans[0], 15, 22, FilterType.Identifier));
        Assert.True(CheckSpan(filtered.Spans[1], 23, 30, FilterType.Identifier));
    }

    [Fact]
    public void Id10()
    {
        var filtered = Default().Filter(GetPolicy(), "context", Piece, "the id is H3SNPUHYEE7JD3H in california.");
        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 10, 25, FilterType.Identifier));
    }

    [Fact]
    public void Id11()
    {
        var filtered = Default().Filter(GetPolicy(), "context", Piece, "the id is 86637729 in california.");
        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 10, 18, FilterType.Identifier));
    }

    [Fact]
    public void Id12()
    {
        var filtered = Default().Filter(GetPolicy(), "context", Piece, "the id is 33778376 in california.");
        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 10, 18, FilterType.Identifier));
    }

    [Fact]
    public void Id13_CustomPattern()
    {
        var filtered = Custom(@"\b[A-Z]{4,}\b", true).Filter(GetPolicy(), "context", Piece, "the id is ABCD.");
        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 10, 14, FilterType.Identifier));
    }

    [Fact]
    public void Id14()
    {
        var filtered = Default().Filter(GetPolicy(), "context", Piece, "the id is 123456.");
        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 10, 16, FilterType.Identifier));
    }

    [Fact]
    public void Id15()
    {
        var filtered = Default().Filter(GetPolicy(), "context", Piece,
            "John Smith, patient ID A203493, was seen on February 18.");
        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 23, 30, FilterType.Identifier));
    }

    [Fact]
    public void Id16_CaseInsensitiveDigits()
    {
        var filtered = Custom(@"\b\d{3,8}\b", false).Filter(GetPolicy(), "context", Piece, "The ID is 123456.");
        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 10, 16, FilterType.Identifier));
    }

    [Fact]
    public void Id17_NegatedClassPattern()
    {
        var filtered = Custom("(?i)([^Application Name])(.*)$", false)
            .Filter(GetPolicy(), "context", Piece, "Application Name John Smith");
        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 17, 27, FilterType.Identifier));
    }

    [Fact]
    public void Id18_DashedDigits()
    {
        var filtered = Custom(@"\d{3}-\d{3}-\d{3}", false).Filter(GetPolicy(), "context", Piece, "his id was 123-456-789");
        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 11, 22, FilterType.Identifier));
    }

    private static Phileas.Filters.FilterConfiguration SmallBudgetConfig()
    {
        return new Phileas.Filters.FilterConfiguration.Builder()
            .WithStrategies(new List<Phileas.Filters.AbstractFilterStrategy>
                { new IdentifierFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<Phileas.Policy.IgnoredPattern>())
            .WithWindowSize(3)
            .WithRegexTimeoutMs(200)
            .Build();
    }

    [Fact(Timeout = 5000)]
    public async Task CatastrophicPatternIsAbortedByTheRegexBudget()
    {
        // Nested greedy .* under a bounded repetition, with a trailing 'b' that never appears, so the
        // match cannot succeed and the engine exhausts an enormous backtracking space (unguarded, this
        // runs for many seconds).
        var filter = new IdentifierFilter(SmallBudgetConfig(), "name", "(.*a){16}b", true, 0);
        var input = "the id is " + new string('a', 30) + "!";

        var filtered = await Task.Run(() => filter.Filter(GetPolicy(), "context", Piece, input));

        Assert.Empty(filtered.Spans);
    }

    [Fact]
    public void LegitimatePatternStillMatchesUnderASmallBudget()
    {
        var filter = new IdentifierFilter(SmallBudgetConfig(), "name", Identifier.DefaultIdentifierRegex, true, 0);

        var filtered = filter.Filter(GetPolicy(), "context", Piece, "the id is AB4736021 in california.");

        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 10, 19, FilterType.Identifier));
    }
}
