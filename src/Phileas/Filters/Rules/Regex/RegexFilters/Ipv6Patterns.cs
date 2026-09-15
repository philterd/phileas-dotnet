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
///     The IPv6 address forms, as one alternation with the boundaries that keep a match from starting
///     or stopping partway through something that is not an address. Mirrors the Java
///     <c>Ipv6Patterns</c>, with two deliberate differences noted below.
/// </summary>
internal static class Ipv6Patterns
{
    /// <summary>
    ///     Every IPv6 form: expanded, expanded mixed (six hextets and a dotted quad), compressed,
    ///     IPv4-mapped, and link-local with a zone.
    ///     <para>
    ///         Digits are ASCII: .NET's <c>\d</c> spans every Unicode decimal digit, which would let a
    ///         fullwidth-digit run be read as an address.
    ///     </para>
    ///     <para>
    ///         Unlike the Java alternation this does not accept a bare <c>::</c>. That is the unspecified
    ///         address, and matching it meant any <c>::</c> in prose or code became a span.
    ///     </para>
    /// </summary>
    private const string Alternatives =
        "(([0-9a-f]{1,4}:){7}[0-9a-f]{1,4}"
        + "|([0-9a-f]{1,4}:){6}((25[0-5]|(2[0-4]|1?[0-9])?[0-9])\\.){3}(25[0-5]|(2[0-4]|1?[0-9])?[0-9])"
        + "|([0-9a-f]{1,4}:){1,7}:"
        + "|([0-9a-f]{1,4}:){1,6}:[0-9a-f]{1,4}"
        + "|([0-9a-f]{1,4}:){1,5}(:[0-9a-f]{1,4}){1,2}"
        + "|([0-9a-f]{1,4}:){1,4}(:[0-9a-f]{1,4}){1,3}"
        + "|([0-9a-f]{1,4}:){1,3}(:[0-9a-f]{1,4}){1,4}"
        + "|([0-9a-f]{1,4}:){1,2}(:[0-9a-f]{1,4}){1,5}"
        + "|[0-9a-f]{1,4}:((:[0-9a-f]{1,4}){1,6})"
        + "|:(:[0-9a-f]{1,4}){1,7}"
        + "|fe80:(:[0-9a-f]{0,4}){0,4}%[0-9a-z]+"
        + "|::(ffff(:0{1,4})?:)?((25[0-5]|(2[0-4]|1?[0-9])?[0-9])\\.){3}(25[0-5]|(2[0-4]|1?[0-9])?[0-9])"
        + "|([0-9a-f]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1?[0-9])?[0-9])\\.){3}(25[0-5]|(2[0-4]|1?[0-9])?[0-9]))";

    /// <summary>An optional zone identifier, such as the <c>%eth0</c> of <c>fe80::1%eth0</c>.</summary>
    private const string Zone = "(?:%[0-9a-z]+)?";

    /// <summary>
    ///     A match may not begin partway through a token. Without this, any <c>::</c> is an address:
    ///     <c>std::vector</c> yielded <c>d::</c> and <c>Foo::Bar</c> yielded <c>::Ba</c>. The class is
    ///     ASCII so an address inside non-Latin text is still found.
    /// </summary>
    private const string Before = "(?<![0-9A-Za-z_:%.])";

    /// <summary>
    ///     The alternation is ordered and the engine takes the first alternative that matches rather
    ///     than the longest, so a compressed address was matched only as far as its <c>::</c>. This
    ///     rejects a match that stopped inside an address, so the engine backtracks into one that
    ///     consumes all of it. A period not followed by a digit still ends the match, so a sentence's
    ///     trailing period is left out rather than blocking the match.
    /// </summary>
    private const string After = "(?![0-9a-f])(?!:[0-9a-f])(?!\\.[0-9])";

    /// <summary>A complete address: the boundary, any form, an optional zone, and the trailing boundary.</summary>
    public const string Address = Before + Alternatives + Zone + After;
}
