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
using Phileas.Services.Validators;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Rules.Regex.RegexFilters;

/// <summary>
///     Regex-based filter that detects IBAN code entities in plain text. A structural match is kept only
///     when it passes the IBAN MOD-97-10 checksum, so an IBAN-shaped string with wrong check digits is
///     rejected rather than redacted.
/// </summary>
public class IbanCodeFilter : RegexFilter
{
    private static readonly Analyzer CompactAnalyzer = new(
        new FilterPattern.Builder().WithPattern(@"\b[A-Z]{2}[0-9]{2}[A-Z0-9]{4}[0-9]{7}([A-Z0-9]?){0,16}\b")
            .WithInitialConfidence(0.90).Build()
    );

    /// <summary>Also matches a code written in the four-character groups banks print.</summary>
    private static readonly Analyzer SpacedAnalyzer = new(
        new FilterPattern.Builder()
            .WithPattern(@"\b[A-Z]{2}[0-9]{2}[\s]?[A-Z0-9]{4}[\s]?[A-Z0-9]{4}[\s]?[A-Z0-9]{4}[\s]?[A-Z0-9]{4}[\s]?[A-Z0-9]{0,2}\b",
                RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder().WithPattern(@"\b[A-Z]{2}[0-9]{2}[A-Z0-9]{4}[0-9]{7}([A-Z0-9]?){0,16}\b")
            .WithInitialConfidence(0.90).Build()
    );

    private readonly bool _allowSpaces;
    private readonly bool _onlyValidIbanCodes;

    /// <summary>
    ///     Initializes a new <see cref="IbanCodeFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    /// <param name="onlyValidIbanCodes">Keep only codes passing the MOD-97-10 checksum.</param>
    /// <param name="allowSpaces">Also detect a code written in space-separated groups.</param>
    public IbanCodeFilter(FilterConfiguration configuration, bool onlyValidIbanCodes = true,
        bool allowSpaces = true) : base(FilterType.IbanCode, configuration)
    {
        _onlyValidIbanCodes = onlyValidIbanCodes;
        _allowSpaces = allowSpaces;
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, _allowSpaces ? SpacedAnalyzer : CompactAnalyzer, input, context, piece);

        // Confirm the structural match with the shared IBAN MOD-97-10 checksum; drop shapes that fail
        // it. Turning the check off keeps every structurally IBAN-shaped value, wrong digits included.
        if (_onlyValidIbanCodes)
            spans = spans.Where(span => Mod97Validator.IsValidIban(span.Text.Replace(" ", string.Empty)))
                .ToList();
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}