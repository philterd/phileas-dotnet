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

using Phileas.Filters.Rules.Dictionary;
using Phileas.Model;
using Xunit;
using static Phileas.Tests.Dictionaries.DictionaryTestSupport;

namespace Phileas.Tests.Dictionaries;

public class FuzzyDictionaryFilterTests
{
    private static readonly HashSet<string> Names = new() { "George", "Ted", "Bill", "John" };

    [Fact]
    public void OffExactMatch()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.CustomDictionary, Config(), SensitivityLevel.Off, Names, false);
        var filtered = filter.Filter(GetPolicy(), "context", Piece, "He lived with Bill in California.");

        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 14, 18, FilterType.CustomDictionary));
        Assert.Equal("Bill", filtered.Spans[0].Text);
    }

    [Fact]
    public void HighRequiresExact()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.CustomDictionary, Config(), SensitivityLevel.High, Names, false);

        Assert.Single(filter.Filter(GetPolicy(), "context", Piece, "He lived with Bill in California.").Spans);
        // HIGH requires distance 0, so a near miss does not match.
        Assert.Empty(filter.Filter(GetPolicy(), "context", Piece, "He lived with Billi in California.").Spans);
    }

    [Fact]
    public void RequireCapitalization()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.CustomDictionary, Config(), SensitivityLevel.Off, Names, true);

        Assert.Single(filter.Filter(GetPolicy(), "context", Piece, "He lived with Bill in California.").Spans);
        // Lowercase "bill" fails the capitalization requirement.
        Assert.Empty(filter.Filter(GetPolicy(), "context", Piece, "He lived with bill in California.").Spans);
    }

    [Fact]
    public void MediumAllowsDistanceOne()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.CustomDictionary, Config(), SensitivityLevel.Medium, Names, true);
        var filtered = filter.Filter(GetPolicy(), "context", Piece, "He lived with Billi in California.");

        Assert.Single(filtered.Spans);
        Assert.Equal("Billi", filtered.Spans[0].Text);
        Assert.Equal(0.7, filtered.Spans[0].Confidence);
    }

    [Fact]
    public void LowAllowsDistanceTwo()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.CustomDictionary, Config(), SensitivityLevel.Low, Names, false);
        var filtered = filter.Filter(GetPolicy(), "context", Piece, "He lived with Billie in California.");

        Assert.Single(filtered.Spans);
        Assert.Equal("Billie", filtered.Spans[0].Text);
        Assert.Equal(0.5, filtered.Spans[0].Confidence);
    }

    [Fact]
    public void CapitalizationGatesFuzzyMatch()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.CustomDictionary, Config(), SensitivityLevel.Medium, Names, true);

        Assert.Empty(filter.Filter(GetPolicy(), "context", Piece, "He lived with billi in California.").Spans);
        Assert.Single(filter.Filter(GetPolicy(), "context", Piece, "He lived with Billi in California.").Spans);
    }

    [Fact]
    public void NoCapitalizationRequirementMatchesLowercase()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.CustomDictionary, Config(), SensitivityLevel.Medium, Names, false);
        var filtered = filter.Filter(GetPolicy(), "context", Piece, "He lived with billi in California.");

        Assert.Single(filtered.Spans);
        Assert.Equal("billi", filtered.Spans[0].Text);
    }
}
