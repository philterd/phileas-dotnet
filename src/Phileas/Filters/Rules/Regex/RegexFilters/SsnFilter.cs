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
    ///     The separator between the groups of an SSN is optional: a hyphen (wrapped across a line
    ///     break or not), one horizontal space, or nothing at all. The fragments are shared with
    ///     <see cref="EinFilter" /> through <see cref="IdentifierSeparators" />.
    /// </summary>
    private const string Separator = IdentifierSeparators.OptionalSeparator;

    private const string Digit = IdentifierSeparators.Digit;

    private static readonly Analyzer SsnAnalyzer = new(
        new FilterPattern.Builder()
            .WithPattern(IdentifierSeparators.NotWordBefore
                         + "(?!000|666|9" + Digit + "{2})" + Digit + "{3}" + Separator
                         + "(?!00)" + Digit + "{2}" + Separator
                         + "(?!0000)" + Digit + "{4}"
                         + IdentifierSeparators.NotWordAfter)
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
