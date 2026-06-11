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

public class SetDictionaryFilterTests
{
    [Fact]
    public void ExactMatch()
    {
        var names = new HashSet<string> { "george", "ted", "Bill", "john" };
        var filter = new SetDictionaryFilter(FilterType.CustomDictionary, Config(), names, "none");
        var filtered = filter.Filter(GetPolicy(), "context", Piece, "He lived with Bill in California.");

        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 14, 18, FilterType.CustomDictionary));
        Assert.Equal("Bill", filtered.Spans[0].Text);
    }

    [Fact]
    public void CaseInsensitiveMatch()
    {
        var names = new HashSet<string> { "george", "ted", "bill", "john" };
        var filter = new SetDictionaryFilter(FilterType.CustomDictionary, Config(), names, "none");
        var filtered = filter.Filter(GetPolicy(), "context", Piece, "He lived with Bill in California.");

        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 14, 18, FilterType.CustomDictionary));
        Assert.Equal("Bill", filtered.Spans[0].Text);
    }

    [Fact]
    public void NoMatch()
    {
        var names = new HashSet<string> { "george", "ted", "bill", "john" };
        var filter = new SetDictionaryFilter(FilterType.CustomDictionary, Config(), names, "none");
        var filtered = filter.Filter(GetPolicy(), "context", Piece, "He lived with Sam in California.");

        Assert.Empty(filtered.Spans);
    }

    [Fact]
    public void PhraseMatch1()
    {
        var names = new HashSet<string> { "george jones", "ted", "bill", "john" };
        var filter = new SetDictionaryFilter(FilterType.CustomDictionary, Config(), names, "none");
        var filtered = filter.Filter(GetPolicy(), "context", Piece, "He lived with george jones in California.");

        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 14, 26, FilterType.CustomDictionary));
        Assert.Equal("george jones", filtered.Spans[0].Text);
    }

    [Fact]
    public void PhraseMatch2()
    {
        var names = new HashSet<string> { "george jones jr", "ted", "bill smith", "john" };
        var filter = new SetDictionaryFilter(FilterType.CustomDictionary, Config(), names, "none");
        var filtered =
            filter.Filter(GetPolicy(), "context", Piece, "Bill Smith lived with george jones jr in California.");

        Assert.Equal(2, filtered.Spans.Count);
        Assert.All(filtered.Spans, s => Assert.True(s.Text is "george jones jr" or "Bill Smith"));
    }

    [Fact]
    public void PhraseMatchMultipleMatches()
    {
        var names = new HashSet<string> { "george jones", "ted", "bill", "john" };
        var filter = new SetDictionaryFilter(FilterType.CustomDictionary, Config(), names, "none");
        var filtered = filter.Filter(GetPolicy(), "context", Piece,
            "He lived with george jones and george jones in California.");

        Assert.Equal(2, filtered.Spans.Count);
        Assert.All(filtered.Spans, s => Assert.Equal("george jones", s.Text));
    }

    [Fact]
    public void FromBundledDictionaryFile()
    {
        var filter = new SetDictionaryFilter(FilterType.LocationCity, Config());
        var filtered = filter.Filter(GetPolicy(), "context", Piece, "He visited Boston.");

        Assert.Contains(filtered.Spans, s => s.Text == "Boston");
    }
}
