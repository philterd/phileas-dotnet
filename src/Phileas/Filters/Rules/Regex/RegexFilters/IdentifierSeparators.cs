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
///     Regular-expression fragments shared by the filters that detect hyphenated numeric
///     identifiers, currently <see cref="SsnFilter" />, <see cref="EinFilter" />,
///     <see cref="ItinFilter" />, <see cref="CanadaSinFilter" /> and <see cref="ZipCodeFilter" />. They live in one place so they cannot drift apart over what
///     separates an identifier's digit groups: a value a reader would write the same way should be
///     detected the same way by each.
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
    ///     The hyphens that join digits into one token: <see cref="HyphenCharacters" /> without the
    ///     en dash, em dash and horizontal bar (U+2013 to U+2015) and the small em dash, which are
    ///     punctuation between words rather than part of an identifier. Mirrors Java's
    ///     <c>IdentifierSeparators.JOINING_HYPHENS</c>.
    /// </summary>
    public const string JoiningHyphens = @"\-\u00AD\u2010-\u2012\u2212\uFE63\uFF0D";

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
    ///     Joins an identifier's three digit groups. Both separators are the same kind: a hyphen,
    ///     wrapped or not, one horizontal space, or nothing. Allowing each to be chosen on its own let
    ///     a ZIP+4 such as <c>52168-1208</c> match as an SSN, with nothing after the first group and a
    ///     hyphen after the second (#177, as philterd/phileas#460).
    /// </summary>
    public static string Join(string first, string second, string third)
    {
        return first + "(?:" + Wrap + second + Wrap + "|" + HorizontalSpace + second + HorizontalSpace + "|" + second
               + ")" + third;
    }

    /// <summary>
    ///     Joins single-digit patterns with one horizontal space each: an identifier written one digit
    ///     to a box, as in the comb fields for the SSN on Form 1040, comes out of a PDF with a space
    ///     between every digit. Each pattern matches one digit and may start with a lookahead, which
    ///     sees the spaces too. A match may not be part of a longer run of spaced digits.
    /// </summary>
    public static string SpacedDigits(params string[] digits)
    {
        return NotWordBefore + "(?<!" + Digit + HorizontalSpace + ")"
               + string.Join(HorizontalSpace, digits)
               + "(?!" + HorizontalSpace + Digit + ")" + NotWordAfter;
    }

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
    ///     found in <c>45-6789123</c> out of <c>123-45-6789123-45-6789</c>. Only a
    ///     <see cref="JoiningHyphens" /> hyphen counts, as in Java, so an identifier directly before or
    ///     after an en or em dash is still found.
    /// </summary>
    public const string NotWordOrHyphenBefore = "(?<![0-9A-Za-z_" + JoiningHyphens + "])";

    /// <inheritdoc cref="NotWordOrHyphenBefore" />
    public const string NotWordOrHyphenAfter = "(?![0-9A-Za-z_" + JoiningHyphens + "])";
}
