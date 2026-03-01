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

using Phileas.Filters;
using Phileas.Filters.PostFilters;
using Phileas.Policy.Filters.Regex;
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Strategies.Rules;
using PhileasPolicy = Phileas.Policy.Policy;
using Xunit;

namespace Phileas.Tests;

public class PostFilterTests
{
    private static Span MakeSpan(int start, int end, string text) =>
        Span.Make(start, end, FilterType.EmailAddress, "ctx", 1.0, text, "REDACTED", "", false, true, null, 0);

    // ── TrailingNewLinesPostFilter ──────────────────────────────────────────

    [Fact]
    public void TrailingNewLines_TrimsLf()
    {
        var spans = new List<Span> { MakeSpan(0, 5, "abc\n") };
        var result = TrailingNewLinesPostFilter.Apply(spans);
        Assert.Single(result);
        Assert.Equal("abc", result[0].Text);
        Assert.Equal(4, result[0].CharacterEnd);
    }

    [Fact]
    public void TrailingNewLines_TrimsCrLf()
    {
        var spans = new List<Span> { MakeSpan(0, 6, "abc\r\n") };
        var result = TrailingNewLinesPostFilter.Apply(spans);
        Assert.Single(result);
        Assert.Equal("abc", result[0].Text);
        Assert.Equal(4, result[0].CharacterEnd);
    }

    [Fact]
    public void TrailingNewLines_NoTrailingNewline_Unchanged()
    {
        var spans = new List<Span> { MakeSpan(0, 3, "abc") };
        var result = TrailingNewLinesPostFilter.Apply(spans);
        Assert.Single(result);
        Assert.Equal("abc", result[0].Text);
        Assert.Equal(3, result[0].CharacterEnd);
    }

    [Fact]
    public void TrailingNewLines_EmptyList_ReturnsEmpty()
    {
        var result = TrailingNewLinesPostFilter.Apply(new List<Span>());
        Assert.Empty(result);
    }

    // ── TrailingPeriodPostFilter ────────────────────────────────────────────

    [Fact]
    public void TrailingPeriod_TrimsSinglePeriod()
    {
        var spans = new List<Span> { MakeSpan(0, 4, "abc.") };
        var result = TrailingPeriodPostFilter.Apply(spans);
        Assert.Single(result);
        Assert.Equal("abc", result[0].Text);
        Assert.Equal(3, result[0].CharacterEnd);
    }

    [Fact]
    public void TrailingPeriod_TrimsMultiplePeriods()
    {
        var spans = new List<Span> { MakeSpan(0, 6, "abc...") };
        var result = TrailingPeriodPostFilter.Apply(spans);
        Assert.Single(result);
        Assert.Equal("abc", result[0].Text);
        Assert.Equal(3, result[0].CharacterEnd);
    }

    [Fact]
    public void TrailingPeriod_NoTrailingPeriod_Unchanged()
    {
        var spans = new List<Span> { MakeSpan(0, 3, "abc") };
        var result = TrailingPeriodPostFilter.Apply(spans);
        Assert.Single(result);
        Assert.Equal("abc", result[0].Text);
    }

    // ── TrailingSpacePostFilter ─────────────────────────────────────────────

    [Fact]
    public void TrailingSpace_TrimsTrailingSpace()
    {
        var spans = new List<Span> { MakeSpan(0, 5, "abc  ") };
        var result = TrailingSpacePostFilter.Apply(spans);
        Assert.Single(result);
        Assert.Equal("abc", result[0].Text);
        Assert.Equal(3, result[0].CharacterEnd);
    }

    [Fact]
    public void TrailingSpace_NoTrailingSpace_Unchanged()
    {
        var spans = new List<Span> { MakeSpan(0, 3, "abc") };
        var result = TrailingSpacePostFilter.Apply(spans);
        Assert.Single(result);
        Assert.Equal("abc", result[0].Text);
    }

    // ── IgnoredTermsPostFilter ──────────────────────────────────────────────

    [Fact]
    public void IgnoredTerms_RemovesMatchingSpan()
    {
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "abc" };
        var spans = new List<Span> { MakeSpan(0, 3, "abc"), MakeSpan(4, 7, "xyz") };
        var result = IgnoredTermsPostFilter.Apply(spans, ignored);
        Assert.Single(result);
        Assert.Equal("xyz", result[0].Text);
    }

    [Fact]
    public void IgnoredTerms_CaseInsensitiveRemoval()
    {
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ABC" };
        var spans = new List<Span> { MakeSpan(0, 3, "abc") };
        var result = IgnoredTermsPostFilter.Apply(spans, ignored);
        Assert.Empty(result);
    }

    [Fact]
    public void IgnoredTerms_EmptyIgnoredSet_ReturnsAllSpans()
    {
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var spans = new List<Span> { MakeSpan(0, 3, "abc") };
        var result = IgnoredTermsPostFilter.Apply(spans, ignored);
        Assert.Single(result);
    }

    // ── IgnoredPatternsPostFilter ───────────────────────────────────────────

    [Fact]
    public void IgnoredPatterns_RemovesMatchingSpan()
    {
        var patterns = new List<IgnoredPattern>
        {
            new IgnoredPattern { Pattern = @"^\d{3}-\d{2}-\d{4}$", CaseSensitive = false }
        };
        var spans = new List<Span>
        {
            MakeSpan(0, 11, "123-45-6789"),
            MakeSpan(12, 15, "abc")
        };
        var result = IgnoredPatternsPostFilter.Apply(spans, patterns);
        Assert.Single(result);
        Assert.Equal("abc", result[0].Text);
    }

    [Fact]
    public void IgnoredPatterns_CaseInsensitivePattern()
    {
        var patterns = new List<IgnoredPattern>
        {
            new IgnoredPattern { Pattern = "^ABC$", CaseSensitive = false }
        };
        var spans = new List<Span> { MakeSpan(0, 3, "abc") };
        var result = IgnoredPatternsPostFilter.Apply(spans, patterns);
        Assert.Empty(result);
    }

    [Fact]
    public void IgnoredPatterns_CaseSensitivePattern_NoMatch()
    {
        var patterns = new List<IgnoredPattern>
        {
            new IgnoredPattern { Pattern = "^ABC$", CaseSensitive = true }
        };
        var spans = new List<Span> { MakeSpan(0, 3, "abc") };
        var result = IgnoredPatternsPostFilter.Apply(spans, patterns);
        Assert.Single(result);
    }

    [Fact]
    public void IgnoredPatterns_EmptyPatternList_ReturnsAllSpans()
    {
        var patterns = new List<IgnoredPattern>();
        var spans = new List<Span> { MakeSpan(0, 3, "abc") };
        var result = IgnoredPatternsPostFilter.Apply(spans, patterns);
        Assert.Single(result);
    }

    // ── Integration: PostFilter applied via RulesFilter (SsnFilter) ─────────

    [Fact]
    public void SsnFilter_IgnoredTerm_IsNotReturned()
    {
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "123-45-6789" };
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new SsnFilterStrategy() })
            .WithIgnored(ignored)
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        var filter = new SsnFilter(config);
        var policy = new PhileasPolicy { Name = "test", Identifiers = new Identifiers { Ssn = new Ssn() } };
        var result = filter.Filter(policy, "ctx", 0, "SSN: 123-45-6789");
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void SsnFilter_IgnoredPattern_IsNotReturned()
    {
        var patterns = new List<IgnoredPattern>
        {
            new IgnoredPattern { Pattern = @"^\d{3}-\d{2}-\d{4}$", CaseSensitive = false }
        };
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new SsnFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(patterns)
            .Build();
        var filter = new SsnFilter(config);
        var policy = new PhileasPolicy { Name = "test", Identifiers = new Identifiers { Ssn = new Ssn() } };
        var result = filter.Filter(policy, "ctx", 0, "SSN: 123-45-6789");
        Assert.Empty(result.Spans);
    }
}
