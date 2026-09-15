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
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Rules.Regex.RegexFilters;

/// <summary>
///     Regex-based filter that detects US Social Security Number (SSN) entities in plain text.
/// </summary>
public class SsnFilter : RegexFilter
{
    /// <summary>
    ///     Substitutes for the ASCII hyphen: the soft hyphen, the U+2010 dash block (which includes
    ///     the non-breaking hyphen U+2011), the minus sign, and the small and fullwidth forms.
    /// </summary>
    private const string HyphenCharacters = @"-\u00AD\u2010-\u2015\u2212\uFE58\uFE63\uFF0D";

    /// <summary>
    ///     The equivalent of Java's <c>\h</c>, which .NET has no shorthand for. It excludes line
    ///     breaks, unlike <c>\s</c>, so a line break on its own no longer separates digit groups.
    /// </summary>
    private const string HorizontalSpace = @"[ \t\u00A0\u1680\u180E\u2000-\u200A\u202F\u205F\u3000]";

    /// <summary>The equivalent of Java's <c>\R</c>, likewise absent from .NET.</summary>
    private const string LineBreak = @"(?:\r\n|[\n\u000B\f\r\u0085\u2028\u2029])";

    /// <summary>
    ///     A hyphen the identifier may be wrapped across. Requiring the hyphen before the line break
    ///     keeps three numbers on three lines from matching. Horizontal space on either side of the
    ///     break allows an indented continuation line. Whitespace is never part of a digit group, so
    ///     the runs are atomic.
    /// </summary>
    private const string Wrap = "[" + HyphenCharacters + "](?>" + HorizontalSpace + "*)"
                                + "(?>(?:" + LineBreak + "(?>" + HorizontalSpace + "*))?)";

    /// <summary>Between the groups of an SSN: a hyphen, wrapped or not, or one horizontal space.</summary>
    private const string Separator = "(?:" + Wrap + "|" + HorizontalSpace + ")?";

    /// <summary>
    ///     Digits are ASCII only, matching the Java filter. .NET's <c>\d</c> spans every Unicode
    ///     decimal digit, which would accept fullwidth and Arabic-Indic forms.
    /// </summary>
    private const string Digit = "[0-9]";

    /// <summary>
    ///     Boundaries, spelled out for the same reason: .NET's <c>\b</c> is Unicode-aware, so an
    ///     identifier butted against a non-ASCII letter would not match.
    /// </summary>
    private const string NotWordBefore = "(?<![0-9A-Za-z_])";

    private const string NotWordAfter = "(?![0-9A-Za-z_])";

    private static readonly Analyzer SsnAnalyzer = new(
        new FilterPattern.Builder()
            .WithPattern(NotWordBefore
                         + "(?!000|666|9" + Digit + "{2})" + Digit + "{3}" + Separator
                         + "(?!00)" + Digit + "{2}" + Separator
                         + "(?!0000)" + Digit + "{4}"
                         + NotWordAfter)
            .WithInitialConfidence(0.90).Build()
    );

    /// <summary>
    ///     Initializes a new <see cref="SsnFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    public SsnFilter(FilterConfiguration configuration) : base(FilterType.Ssn, configuration)
    {
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, SsnAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
