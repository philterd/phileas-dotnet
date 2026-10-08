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
///     Regex-based filter that detects US Individual Taxpayer Identification Number (ITIN) entities in plain text.
///     An ITIN has the SSN's shape and always begins with 9 (<c>9XX-XX-XXXX</c>), a range <see cref="SsnFilter" />
///     rejects, so a value is never reported as both. ATINs (<c>9XX-93-XXXX</c>) have the same shape and are
///     reported as ITINs unless <c>onlyValidRanges</c> is set.
///     <para>
///         The separator and boundary fragments come from <see cref="IdentifierSeparators" />, shared with
///         <see cref="SsnFilter" />, so the two filters accept the same forms: hyphenated, separated by a single
///         space, or unformatted.
///     </para>
/// </summary>
public class ItinFilter : RegexFilter
{
    private const string Separator = IdentifierSeparators.OptionalSeparator;

    private const string Digit = IdentifierSeparators.Digit;

    private static readonly Analyzer ItinAnalyzer = new(
        new FilterPattern.Builder()
            .WithPattern(IdentifierSeparators.NotWordBefore
                         + "9" + Digit + "{2}" + Separator + Digit + "{2}" + Separator + Digit + "{4}"
                         + IdentifierSeparators.NotWordAfter)
            .WithInitialConfidence(0.90).Build()
    );

    private readonly bool _onlyValidRanges;

    /// <summary>
    ///     Initializes a new <see cref="ItinFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    /// <param name="onlyValidRanges">
    ///     When <see langword="true" />, keep only matches whose fourth and fifth digits fall in the ranges the IRS
    ///     issues.
    /// </param>
    public ItinFilter(FilterConfiguration configuration, bool onlyValidRanges = false)
        : base(FilterType.Itin, configuration)
    {
        _onlyValidRanges = onlyValidRanges;
    }

    /// <summary>
    ///     Returns whether the fourth and fifth digits of <paramref name="itin" /> fall in the ranges the IRS issues
    ///     for ITINs: 50 to 65, 70 to 88, 90 to 92, and 94 to 99 (IRS Publication 4757; IRM 3.21.263, which reserves
    ///     89 and 93 for other programs). Separators are ignored.
    /// </summary>
    public static bool IsInValidRange(string itin)
    {
        var digits = itin.Where(char.IsAsciiDigit).ToArray();
        if (digits.Length != 9)
            return false;

        var group = (digits[3] - '0') * 10 + (digits[4] - '0');
        return group is >= 50 and <= 65 or >= 70 and <= 88 or >= 90 and <= 92 or >= 94 and <= 99;
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, ItinAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);

        if (_onlyValidRanges)
            spans = spans.Where(span => IsInValidRange(span.Text)).ToList();

        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
