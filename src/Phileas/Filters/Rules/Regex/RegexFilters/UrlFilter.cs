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

using System.Text.RegularExpressions;
using Phileas.Model;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Rules.Regex.RegexFilters;

/// <summary>
///     Regex-based filter that detects URL entities in plain text.
/// </summary>
public class UrlFilter : RegexFilter
{
    /// <summary>
    ///     A URL span must end on a character a URL may legitimately end on: a word character, or one
    ///     of the URL punctuation that carries meaning in a path, query or fragment. Anything else,
    ///     which is to say the punctuation prose puts after a URL, is left in the document.
    ///     <para>
    ///         This is written as what may end a match rather than what may not, so that punctuation
    ///         outside ASCII is excluded too. Listing the excluded characters instead would swallow the
    ///         ideographic full stop of <c>http://example.com/page</c> followed by U+3002, and every
    ///         other script's sentence punctuation with it.
    ///     </para>
    ///     <para>
    ///         The decision this records: a trailing <c>/</c>, <c>&amp;</c> or <c>#</c> is part of the
    ///         URL and stays in the span. Punctuation inside a path is untouched either way, so
    ///         <c>/a/b.html</c> and <c>?q=1,2</c> match whole.
    ///     </para>
    /// </summary>
    private const string EndsOnUrlCharacter = @"(?<=[\w/#&=~+$*@%-])";

    private static readonly Analyzer PrefixedAnalyzer = new(
        new FilterPattern.Builder().WithPattern(@"\b(?:https?|ftp)://[^\s/$.?#].[^\s]*" + EndsOnUrlCharacter, RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.95).Build(),
        new FilterPattern.Builder().WithPattern(@"\bwww\.[^\s/$.?#].[^\s]*" + EndsOnUrlCharacter, RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).Build()
    );

    /// <summary>
    ///     Also matches a bare host such as <c>example.com/path</c>. The prefix is what separates a URL
    ///     from ordinary prose containing a dot, so dropping the requirement costs precision: a lower
    ///     initial confidence reflects that.
    /// </summary>
    private static readonly Analyzer UnprefixedAnalyzer = new(
        new FilterPattern.Builder().WithPattern(@"\b(?:https?|ftp)://[^\s/$.?#].[^\s]*" + EndsOnUrlCharacter, RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.95).Build(),
        new FilterPattern.Builder().WithPattern(@"\bwww\.[^\s/$.?#].[^\s]*" + EndsOnUrlCharacter, RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder()
            .WithPattern(@"\b[a-z\d]+(?:[\-.][a-z\d]+)*\.[a-z]{2,5}(?::\d{1,5})?(?:/[^\s]*)?" + EndsOnUrlCharacter,
                RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.70).Build()
    );

    private readonly bool _requireHttpWwwPrefix;

    /// <summary>
    ///     Initializes a new <see cref="UrlFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    /// <param name="configuration">Runtime filter configuration.</param>
    /// <param name="requireHttpWwwPrefix">
    ///     Require a <c>http://</c>, <c>https://</c> or <c>www.</c> prefix. When false a bare host is
    ///     also detected.
    /// </param>
    public UrlFilter(FilterConfiguration configuration, bool requireHttpWwwPrefix = true)
        : base(FilterType.Url, configuration)
    {
        _requireHttpWwwPrefix = requireHttpWwwPrefix;
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, _requireHttpWwwPrefix ? PrefixedAnalyzer : UnprefixedAnalyzer,
            input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}