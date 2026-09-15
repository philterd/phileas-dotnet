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
    private static readonly Analyzer PrefixedAnalyzer = new(
        new FilterPattern.Builder().WithPattern(@"\b(?:https?|ftp)://[^\s/$.?#].[^\s]*\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.95).Build(),
        new FilterPattern.Builder().WithPattern(@"\bwww\.[^\s/$.?#].[^\s]*\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).Build()
    );

    /// <summary>
    ///     Also matches a bare host such as <c>example.com/path</c>. The prefix is what separates a URL
    ///     from ordinary prose containing a dot, so dropping the requirement costs precision: a lower
    ///     initial confidence reflects that.
    /// </summary>
    private static readonly Analyzer UnprefixedAnalyzer = new(
        new FilterPattern.Builder().WithPattern(@"\b(?:https?|ftp)://[^\s/$.?#].[^\s]*\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.95).Build(),
        new FilterPattern.Builder().WithPattern(@"\bwww\.[^\s/$.?#].[^\s]*\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder()
            .WithPattern(@"\b[a-z\d]+(?:[\-.][a-z\d]+)*\.[a-z]{2,5}(?::\d{1,5})?(?:/[^\s]*)?",
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