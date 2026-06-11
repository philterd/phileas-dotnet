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
using Phileas.Filters.Strategies.Rules;
using Phileas.Model;
using Xunit;
using static Phileas.Tests.Dictionaries.DictionaryTestSupport;

namespace Phileas.Tests.Dictionaries;

public class CityFilterTests
{
    [Fact]
    public void ExactMatchMedium()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.LocationCity, Config(new CityFilterStrategy()), SensitivityLevel.Medium, true);
        var spans = Span.DropOverlappingSpans(filter.Filter(GetPolicy(), "context", Piece, "Lived in Washington.").Spans);
        Assert.Single(spans);
        Assert.True(CheckSpanInSpans(spans, 9, 19, FilterType.LocationCity, "Washington", "{{{REDACTED-city}}}"));
    }

    [Fact]
    public void ExactMatchHighMultiWord()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.LocationCity, Config(new CityFilterStrategy()), SensitivityLevel.High, true);
        var spans = Span.DropOverlappingSpans(filter.Filter(GetPolicy(), "context", Piece, "Lived in New York.").Spans);
        Assert.Single(spans);
        Assert.True(CheckSpan(spans[0], 9, 17, FilterType.LocationCity));
        Assert.Equal("New York", spans[0].Text);
    }

    [Fact]
    public void Low()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.LocationCity, Config(new CityFilterStrategy()), SensitivityLevel.Low, true);
        var spans = Span.DropOverlappingSpans(filter.Filter(GetPolicy(), "context", Piece, "Lived in Wshington").Spans);
        Assert.Equal(2, spans.Count);
        Assert.True(CheckSpanInSpans(spans, 9, 18, FilterType.LocationCity, "Wshington", "{{{REDACTED-city}}}"));
    }

    [Fact]
    public void Medium()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.LocationCity, Config(new CityFilterStrategy()), SensitivityLevel.Medium, true);
        var spans = Span.DropOverlappingSpans(filter.Filter(GetPolicy(), "context", Piece, "Lived in Wshington").Spans);
        Assert.Single(spans);
        Assert.True(CheckSpanInSpans(spans, 9, 18, FilterType.LocationCity, "Wshington", "{{{REDACTED-city}}}"));
    }

    [Fact]
    public void HighNoMatch()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.LocationCity, Config(new CityFilterStrategy()), SensitivityLevel.High, true);
        var spans = Span.DropOverlappingSpans(filter.Filter(GetPolicy(), "context", Piece, "Lived in Wasinton").Spans);
        Assert.Empty(spans);
    }
}
