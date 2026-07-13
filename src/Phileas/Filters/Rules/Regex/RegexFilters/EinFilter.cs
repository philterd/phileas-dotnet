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
///     Regex-based filter that detects US Employer Identification Number (EIN) entities in plain text. The canonical
///     form is <c>NN-NNNNNNN</c> (two digits, a hyphen, seven digits), matched at word boundaries. The hyphen position
///     distinguishes an EIN from an SSN (<c>NNN-NN-NNNN</c>); an unhyphenated nine-digit run is not claimed here, so
///     bare runs are left to the SSN filter and the shared span-disambiguation step.
/// </summary>
public class EinFilter : RegexFilter
{
    private static readonly Analyzer EinAnalyzer = new(
        new FilterPattern.Builder().WithPattern(@"\b\d{2}-\d{7}\b")
            .WithInitialConfidence(0.90).Build()
    );

    /// <summary>
    ///     The two-digit prefixes the IRS currently issues (the campus/online codes from the IRS "How EINs are
    ///     Assigned and Valid EIN Prefixes" list). Used only when <c>onlyValidPrefixes</c> is enabled.
    /// </summary>
    private static readonly HashSet<string> ValidPrefixes = new()
    {
        "01", "02", "03", "04", "05", "06",
        "10", "11", "12", "13", "14", "15", "16",
        "20", "21", "22", "23", "24", "25", "26", "27",
        "30", "31", "32", "33", "34", "35", "36", "37", "38", "39",
        "40", "41", "42", "43", "44", "45", "46", "47", "48",
        "50", "51", "52", "53", "54", "55", "56", "57", "58", "59",
        "60", "61", "62", "63", "64", "65", "66", "67", "68",
        "71", "72", "73", "74", "75", "76", "77",
        "80", "81", "82", "83", "84", "85", "86", "87", "88",
        "90", "91", "92", "93", "94", "95", "98", "99"
    };

    private readonly bool _onlyValidPrefixes;

    /// <summary>
    ///     Initializes a new <see cref="EinFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    /// <param name="onlyValidPrefixes">
    ///     When <see langword="true" />, keep only matches whose two-digit prefix is one the IRS currently issues.
    /// </param>
    public EinFilter(FilterConfiguration configuration, bool onlyValidPrefixes = false)
        : base(FilterType.Ein, configuration)
    {
        _onlyValidPrefixes = onlyValidPrefixes;
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, EinAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);

        if (_onlyValidPrefixes)
            spans = spans.Where(span => span.Text.Length >= 2 && ValidPrefixes.Contains(span.Text[..2])).ToList();

        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
