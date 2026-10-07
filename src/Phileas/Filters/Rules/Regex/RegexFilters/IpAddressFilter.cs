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
///     Regex-based filter that detects IP address entities in plain text: IPv4 dotted quads, and IPv6
///     in every written form through <see cref="Ipv6Patterns" />.
///     <para>
///         A bare <c>::</c> is not treated as an address. It is the unspecified address, but matching it
///         meant every <c>::</c> in prose or code became a span, so <c>std::vector</c> was reported as an
///         IP address.
///     </para>
///     <para>
///         An IPv4 address with a letter or underscore against it, such as the <c>1.2.3.4</c> of
///         <c>v1.2.3.4</c>, is still detected, by a second pattern that only requires no digit on either
///         side. Both report 0.9, the Java port's confidence for any IPv4 address
///         (philterd/phileas-dotnet#159).
///     </para>
/// </summary>
public class IpAddressFilter : RegexFilter
{
    private const string Ipv4 =
        @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)";

    /// <summary>
    ///     The strict form of each address is listed first and the relaxed form second, because
    ///     <see cref="Analyzer" /> patterns are applied in order and a span already found at exactly
    ///     the same offsets is not added again. Only an address that abuts other text is found by the
    ///     relaxed form.
    /// </summary>
    private static readonly Analyzer IpAnalyzer = new(
        new FilterPattern.Builder().WithPattern(@"\b" + Ipv4 + @"\b")
            .WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder().WithPattern(Ipv6Patterns.Address, RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).Build(),

        // An address with a letter or underscore against it, such as the "1.2.3.4" of "v1.2.3.4",
        // is still an address and is still redacted. Java's single IPv4 pattern finds it at 0.9 too.
        new FilterPattern.Builder().WithPattern(@"(?<![0-9])" + Ipv4 + @"(?![0-9])")
            .WithInitialConfidence(0.90).Build()
    );

    /// <summary>
    ///     Initializes a new <see cref="IpAddressFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    public IpAddressFilter(FilterConfiguration configuration) : base(FilterType.IpAddress, configuration)
    {
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, IpAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}