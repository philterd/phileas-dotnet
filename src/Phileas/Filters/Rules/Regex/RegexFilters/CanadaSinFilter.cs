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
using Phileas.Policy.Filters;
using Phileas.Services.Validators;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Rules.Regex.RegexFilters;

/// <summary>
///     Regex-based filter that detects Canadian Social Insurance Number (SIN) entities in plain text: nine digits,
///     unformatted or in three groups of three (<c>NNN-NNN-NNN</c>). Every match must pass the Luhn check, which is
///     part of the number's definition and so cannot be turned off.
///     <para>
///         The separator and boundary fragments come from <see cref="IdentifierSeparators" />, shared with
///         <see cref="SsnFilter" />, so the two filters agree on which hyphens count, on an identifier wrapped across
///         a line break, and on what a digit is. An unformatted nine-digit run can match both; the shared
///         span-disambiguation step decides between them, with no SIN-specific rule.
///     </para>
/// </summary>
public class CanadaSinFilter : RegexFilter
{
    /// <summary>Words near a match that raise its confidence, in English and French (philterd/phisql#61).</summary>
    private static readonly HashSet<string> ContextualTerms = new() { "sin", "social insurance", "nas", "assurance sociale" };

    private const string Separator = IdentifierSeparators.OptionalSeparator;

    private const string Digit = IdentifierSeparators.Digit;

    private static readonly Analyzer CanadaSinAnalyzer = new(
        ContextualTerms,
        new FilterPattern.Builder()
            .WithPattern(IdentifierSeparators.NotWordBefore
                         + Digit + "{3}" + Separator + Digit + "{3}" + Separator + Digit + "{3}"
                         + IdentifierSeparators.NotWordAfter)
            .WithInitialConfidence(0.90).Build()
    );

    private readonly bool _onlyValidPrefixes;

    /// <summary>
    ///     Initializes a new <see cref="CanadaSinFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    /// <param name="onlyValidPrefixes">
    ///     When <see langword="true" />, drop matches beginning with 0 or 8, neither of which is issued as a personal
    ///     SIN.
    /// </param>
    public CanadaSinFilter(FilterConfiguration configuration, bool onlyValidPrefixes = false)
        : base(FilterType.CanadaSin, configuration)
    {
        _onlyValidPrefixes = onlyValidPrefixes;
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, CanadaSinAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);

        spans = spans.Where(span => LuhnValidator.IsValid(span.Text)).ToList();

        // A match begins with an ASCII digit, so its first character is the SIN's first digit.
        if (_onlyValidPrefixes)
            spans = spans.Where(span => span.Text[0] != '0' && span.Text[0] != '8').ToList();

        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
