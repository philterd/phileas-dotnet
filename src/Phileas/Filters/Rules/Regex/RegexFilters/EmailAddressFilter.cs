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
///     Regex-based filter that detects email address entities in plain text.
/// </summary>
public class EmailAddressFilter : RegexFilter
{
    /// <summary>
    ///     The strict form follows RFC 5322's unquoted local part, so it accepts the specials the RFC
    ///     permits. "Strict" means strictly conformant, not narrower: it matches more, not less.
    /// </summary>
    private static readonly Analyzer StrictAnalyzer = new(
        new FilterPattern.Builder()
            .WithPattern(@"\b[A-Za-z0-9!#$%&'*+/=?^_`{|}~.\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b")
            .WithInitialConfidence(0.99)
            .Build()
    );

    /// <summary>The lenient form allows only word characters, dots and dashes in the local part.</summary>
    private static readonly Analyzer LenientAnalyzer = new(
        new FilterPattern.Builder()
            .WithPattern(@"\b[\w.\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b")
            .WithInitialConfidence(0.99)
            .Build()
    );

    private readonly bool _onlyStrictMatches;
    private readonly bool _onlyValidTlds;

    /// <summary>
    ///     Initializes a new <see cref="EmailAddressFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    /// <param name="configuration">Runtime filter configuration.</param>
    /// <param name="onlyStrictMatches">Use the RFC-conformant local part instead of the lenient one.</param>
    /// <param name="onlyValidTlds">Keep only addresses whose top-level domain is IANA-registered.</param>
    public EmailAddressFilter(FilterConfiguration configuration, bool onlyStrictMatches = true,
        bool onlyValidTlds = false) : base(FilterType.EmailAddress, configuration)
    {
        _onlyStrictMatches = onlyStrictMatches;
        _onlyValidTlds = onlyValidTlds;
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, _onlyStrictMatches ? StrictAnalyzer : LenientAnalyzer,
            input, context, piece);

        if (_onlyValidTlds)
            spans = spans.Where(span => TopLevelDomains.IsRegistered(span.Text)).ToList();

        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}