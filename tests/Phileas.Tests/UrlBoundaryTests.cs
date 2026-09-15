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

using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
///     Where a URL span ends. See philterd/phileas-dotnet#78.
/// </summary>
public class UrlBoundaryTests
{
    private static IList<Span> Detect(string input, bool requirePrefix = true)
    {
        return new FilterService().Filter(
            new PhileasPolicy
            {
                Name = "t",
                Identifiers = new Identifiers { Url = new Url { RequireHttpWwwPrefix = requirePrefix } }
            },
            "ctx", 0, input).Spans;
    }

    [Theory]
    [InlineData("http://example.com/path/")] // the trailing slash is part of the URL
    [InlineData("http://example.com/")]
    [InlineData("http://example.com/s?q=1&")]
    [InlineData("http://example.com/p#")]
    [InlineData("https://example.com/a/b.html")]
    [InlineData("http://example.com/s?q=1,2")] // punctuation inside a path is kept
    [InlineData("ftp://example.com/dir/")]
    public void AUrlKeepsEveryCharacterThatBelongsToIt(string url)
    {
        var input = "see " + url + " now";

        var span = Assert.Single(Detect(input));
        Assert.Equal(url, span.Text);
        Assert.Equal(url, input[span.CharacterStart..span.CharacterEnd]);
    }

    [Theory]
    [InlineData("see http://example.com/page, then", "http://example.com/page")]
    [InlineData("see http://example.com/page. Then", "http://example.com/page")]
    [InlineData("see (http://example.com/page) here", "http://example.com/page")]
    [InlineData("quote \"http://example.com/page.\" end", "http://example.com/page")]
    [InlineData("list http://example.com/page; next", "http://example.com/page")]
    [InlineData("really? http://example.com/page!", "http://example.com/page")]
    [InlineData("trail http://example.com/path... end", "http://example.com/path")]
    public void SentencePunctuationAfterAUrl_StaysOutOfTheSpan(string input, string expected)
    {
        var span = Assert.Single(Detect(input));

        Assert.Equal(expected, span.Text);
    }

    [Theory]
    [InlineData("\u3002")] // ideographic full stop
    [InlineData("\u3001")] // ideographic comma
    [InlineData("\uff09")] // fullwidth right parenthesis
    [InlineData("\u2014")] // em dash
    [InlineData("\u201d")] // right double quotation mark
    [InlineData("\u06d4")] // Arabic full stop
    public void PunctuationOutsideAsciiAlsoStaysOutOfTheSpan(string punctuation)
    {
        // The boundary is written as what may end a URL rather than what may not, so every script's
        // sentence punctuation is excluded and not just the ASCII set.
        var span = Assert.Single(Detect("see http://example.com/page" + punctuation + " end"));

        Assert.Equal("http://example.com/page", span.Text);
    }

    [Theory]
    [InlineData("see http://example.com/path/")] // last thing in the input
    [InlineData("http://example.com/path/ is the link")] // first thing
    [InlineData("http://example.com/path/")] // the whole input
    public void AUrlAtTheEdgeOfTheInput_IsStillWhole(string input)
    {
        var span = Assert.Single(Detect(input));

        Assert.Equal("http://example.com/path/", span.Text);
    }

    [Fact]
    public void AWwwUrlKeepsItsTrailingSlash()
    {
        var span = Assert.Single(Detect("visit www.example.com/ now"));

        Assert.Equal("www.example.com/", span.Text);
    }

    [Fact]
    public void ABareHostKeepsItsTrailingSlashWhenThePrefixIsNotRequired()
    {
        var span = Assert.Single(Detect("visit example.com/page/ now", requirePrefix: false));

        Assert.Equal("example.com/page/", span.Text);
    }

    [Fact]
    public void ABareHostStillExcludesSentencePunctuation()
    {
        var span = Assert.Single(Detect("visit example.com/page. Then", requirePrefix: false));

        Assert.Equal("example.com/page", span.Text);
    }
}
