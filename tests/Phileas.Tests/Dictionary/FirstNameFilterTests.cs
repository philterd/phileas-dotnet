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

public class FirstNameFilterTests
{
    private static FuzzyDictionaryFilter Filter(SensitivityLevel level, bool requireCap) =>
        new(FilterType.FirstName, Config(), level, requireCap);

    [Fact]
    public void Low() =>
        Assert.Single(Span.DropOverlappingSpans(Filter(SensitivityLevel.Low, true).Filter(GetPolicy(), "context", Piece, "John").Spans));

    [Fact]
    public void Medium1() =>
        Assert.Single(Span.DropOverlappingSpans(Filter(SensitivityLevel.Medium, true).Filter(GetPolicy(), "context", Piece, "Michel had eye cancer").Spans));

    [Fact]
    public void Medium2() =>
        Assert.Single(Span.DropOverlappingSpans(Filter(SensitivityLevel.Low, true).Filter(GetPolicy(), "context", Piece, "Jennifer had eye cancer").Spans));

    [Fact]
    public void Melissa() =>
        Assert.Single(Span.DropOverlappingSpans(Filter(SensitivityLevel.Medium, true).Filter(GetPolicy(), "context", Piece, "Melissa").Spans));

    [Fact]
    public void CaseSensitiveWhenRequired()
    {
        var filter = Filter(SensitivityLevel.Low, true);
        Assert.Single(Span.DropOverlappingSpans(filter.Filter(GetPolicy(), "context", Piece, "Thomas").Spans));
        Assert.Empty(filter.Filter(GetPolicy(), "context", Piece, "thomas").Spans);
    }

    [Fact]
    public void Low2() =>
        Assert.Single(Span.DropOverlappingSpans(Filter(SensitivityLevel.Low, true).Filter(GetPolicy(), "context", Piece, "John").Spans));

    [Fact]
    public void High_TwoExactNames() =>
        // Both "Sandra" and "Washington" are entries in the bundled first-names list.
        Assert.Equal(2, Filter(SensitivityLevel.High, true).Filter(GetPolicy(), "context", Piece, "Sandra in Washington").Spans.Count);

    [Fact]
    public void Low_CommaSeparatedNames() =>
        Assert.Equal(3, Span.DropOverlappingSpans(Filter(SensitivityLevel.Low, true).Filter(GetPolicy(), "context", Piece, "Smith,Melissa A,MD").Spans).Count);

    [Fact]
    public void Low_NoCapitalization_Dat() =>
        Assert.Single(Span.DropOverlappingSpans(Filter(SensitivityLevel.Low, false).Filter(GetPolicy(), "context", Piece, "dat").Spans));

    [Fact]
    public void Low_NoCapitalization_Joie() =>
        Assert.Single(Span.DropOverlappingSpans(Filter(SensitivityLevel.Low, false).Filter(GetPolicy(), "context", Piece, "joie").Spans));
}
