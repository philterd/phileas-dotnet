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

public class SurnameFilterTests
{
    [Fact]
    public void Low()
    {
        // The original Java test used "Wshington" against a larger surnames file; the bundled 100-surname
        // file has no near-match there, so this exercises the same LOW fuzzy match against a real surname.
        var filter = new FuzzyDictionaryFilter(FilterType.Surname, Config(), SensitivityLevel.Low, true);
        var spans = Span.DropOverlappingSpans(filter.Filter(GetPolicy(), "context", Piece, "Lived in Jhnson").Spans);
        Assert.Single(spans);
        Assert.Equal("Jhnson", spans[0].Text);
    }

    [Fact]
    public void Medium()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.Surname, Config(), SensitivityLevel.Medium, true);
        var spans = Span.DropOverlappingSpans(filter.Filter(GetPolicy(), "context", Piece, "Lived in Jhnson").Spans);
        Assert.Single(spans);
    }

    [Fact]
    public void High()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.Surname, Config(), SensitivityLevel.High, true);
        var spans = Span.DropOverlappingSpans(filter.Filter(GetPolicy(), "context", Piece, "Jones").Spans);
        Assert.Single(spans);
    }

    [Fact]
    public void LowNoCapitalization()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.Surname, Config(), SensitivityLevel.Low, false);
        var spans = Span.DropOverlappingSpans(filter.Filter(GetPolicy(), "context", Piece, "date").Spans);
        Assert.Single(spans);
    }

    [Fact]
    public void LowCapitalized()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.Surname, Config(), SensitivityLevel.Low, true);
        var spans = Span.DropOverlappingSpans(filter.Filter(GetPolicy(), "context", Piece, "Jones").Spans);
        Assert.Single(spans);
    }
}
