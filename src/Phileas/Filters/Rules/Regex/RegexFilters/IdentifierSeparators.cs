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

namespace Phileas.Filters.Rules.Regex.RegexFilters;

/// <summary>
///     Regular-expression fragments shared by the filters that detect hyphenated US numeric
///     identifiers, currently <see cref="SsnFilter" /> and <see cref="EinFilter" />. They live in one
///     place so the two cannot drift apart over what separates an identifier's digit groups: a value
///     a reader would write the same way should be detected the same way by both.
/// </summary>
internal static class IdentifierSeparators
{
    /// <summary>
    ///     Substitutes for the ASCII hyphen: the soft hyphen, the U+2010 dash block (which includes
    ///     the non-breaking hyphen U+2011), the minus sign, and the small and fullwidth forms. The
    ///     ASCII hyphen is escaped so the class can be concatenated into a larger one without the
    ///     preceding character forming a range.
    /// </summary>
    public const string HyphenCharacters = @"\-\u00AD\u2010-\u2015\u2212\uFE58\uFE63\uFF0D";

    /// <summary>
    ///     The equivalent of Java's <c>\h</c>, which .NET has no shorthand for. It excludes line
    ///     breaks, unlike <c>\s</c>, so a line break on its own never separates digit groups.
    /// </summary>
    public const string HorizontalSpace = @"[ \t\u00A0\u1680\u180E\u2000-\u200A\u202F\u205F\u3000]";

    /// <summary>The equivalent of Java's <c>\R</c>, likewise absent from .NET.</summary>
    public const string LineBreak = @"(?:\r\n|[\n\u000B\f\r\u0085\u2028\u2029])";

    /// <summary>
    ///     A hyphen the identifier may be wrapped across. Requiring the hyphen before the line break
    ///     keeps unrelated numbers on consecutive lines from joining up. Horizontal space on either
    ///     side of the break allows an indented continuation line. Whitespace is never part of a
    ///     digit group, so the runs are atomic.
    /// </summary>
    public const string Wrap = "[" + HyphenCharacters + "](?>" + HorizontalSpace + "*)"
                               + "(?>(?:" + LineBreak + "(?>" + HorizontalSpace + "*))?)";

    /// <summary>
    ///     A hyphen, wrapped or not, or one horizontal space, or nothing. Used where a separator is
    ///     optional, as between the groups of an SSN.
    /// </summary>
    public const string OptionalSeparator = "(?:" + Wrap + "|" + HorizontalSpace + ")?";

    /// <summary>
    ///     Digits are ASCII only, matching the Java filters. .NET's <c>\d</c> spans every Unicode
    ///     decimal digit, which would accept fullwidth, Arabic-Indic and Devanagari forms.
    /// </summary>
    public const string Digit = "[0-9]";

    /// <summary>
    ///     Boundaries, spelled out for the same reason: .NET's <c>\b</c> is Unicode-aware, so an
    ///     identifier butted against a non-ASCII letter would not match.
    /// </summary>
    public const string NotWordBefore = "(?<![0-9A-Za-z_])";

    /// <inheritdoc cref="NotWordBefore" />
    public const string NotWordAfter = "(?![0-9A-Za-z_])";

    /// <summary>
    ///     A boundary that also rejects an adjacent hyphen, for an identifier whose own separator is
    ///     a hyphen. Without it a fragment straddling two longer identifiers matches: an EIN was
    ///     found in <c>45-6789123</c> out of <c>123-45-6789123-45-6789</c>.
    /// </summary>
    public const string NotWordOrHyphenBefore = "(?<![0-9A-Za-z_" + HyphenCharacters + "])";

    /// <inheritdoc cref="NotWordOrHyphenBefore" />
    public const string NotWordOrHyphenAfter = "(?![0-9A-Za-z_" + HyphenCharacters + "])";
}
