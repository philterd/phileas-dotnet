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
///     Regex-based filter that detects street address entities in plain text.
/// </summary>
public class StreetAddressFilter : RegexFilter
{
    // A leading or trailing directional (pre-name "123 N Main St" or post-suffix quadrant "Main St NW").
    private const string Directional =
        @"(?:NE|NW|SE|SW|N|S|E|W|North|South|East|West|Northeast|Northwest|Southeast|Southwest)";

    // Recognized street-type tokens (longer forms before their abbreviations so the fuller match wins).
    private const string StreetType =
        @"(?:Street|St|Avenue|Ave|Boulevard|Blvd|Drive|Dr|Road|Rd|Lane|Ln|Way|Court|Ct|Place|Pl|" +
        @"Circle|Cir|Highway|Hwy|Parkway|Pkwy|Square|Sq|Trail|Trl|Terrace|Ter|Turnpike|Tpke|" +
        @"Expressway|Expy|Freeway|Fwy|Crossing|Xing|Crescent|Cres|Plaza|Plz|Landing|Lndg|" +
        @"Gardens|Garden|Gdns|Commons|Manor|Mnr|Ridge|Rdg|Point|Pt|Grove|Grv|Alley|Aly|" +
        @"Cove|Cv|Bend|Bnd|Loop|Pike|Path|Mews|Row|Run|Walk|Close)";

    // Optional secondary unit designator, folded into the span (e.g. "Apt 4B", "Suite 200", ", Unit 12", "#5").
    private const string Unit =
        @"(?:[,\s]+(?:Apt|Apartment|Suite|Ste|Unit|Bldg|Building|Floor|Fl|Room|Rm|#)\.?\s*#?\s*[A-Za-z0-9-]+)?";

    // House number (with optional range/letter, e.g. 123, 123-125, 123A), optional pre-directional, 1-5
    // street-name words (allowing ordinals like "5th" and saint/abbreviated forms like "St. Charles"),
    // a street type, then optional post-directional and unit.
    private const string StreetAddressPattern =
        @"\b\d{1,6}(?:-\d{1,6})?[A-Za-z]?\s+" +
        @"(?:" + Directional + @"\s+)?" +
        @"(?:[A-Za-z0-9'.-]+\s+){1,5}" +
        StreetType + @"\b\.?" +
        @"(?:\s+" + Directional + @")?" +
        Unit;

    // Post office box, e.g. "PO Box 1234", "P.O. Box 56", "Post Office Box 789".
    private const string PoBoxPattern = @"\b(?:P\.?\s?O\.?\s?Box|Post\s+Office\s+Box)\s+\d+\b";

    private static readonly Analyzer StreetAddressAnalyzer = new(
        new FilterPattern.Builder().WithPattern(StreetAddressPattern, RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.85).Build(),
        new FilterPattern.Builder().WithPattern(PoBoxPattern, RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.85).Build()
    );

    /// <summary>
    ///     Initializes a new <see cref="StreetAddressFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    public StreetAddressFilter(FilterConfiguration configuration) : base(FilterType.StreetAddress, configuration)
    {
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, StreetAddressAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}