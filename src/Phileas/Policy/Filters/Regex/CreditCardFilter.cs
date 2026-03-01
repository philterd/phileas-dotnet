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
using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Rules.Regex;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Policy.Filters.Regex;

/// <summary>
/// Regex-based filter that detects credit card number entities in plain text.
/// </summary>
public class CreditCardFilter : RegexFilter
{
    private static readonly Analyzer CreditCardAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|3[47][0-9]{13}|6(?:011|5[0-9]{2})[0-9]{12}|3(?:0[0-5]|[68][0-9])[0-9]{11}|(?:2131|1800|35\d{3})\d{11})\b").WithInitialConfidence(0.95).Build(),
        new FilterPattern.Builder().WithPattern(@"\b\d{4}[\s\-]\d{4}[\s\-]\d{4}[\s\-]\d{4}\b").WithInitialConfidence(0.85).Build()
    );

    /// <summary>
    /// Initializes a new <see cref="CreditCardFilter"/> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    public CreditCardFilter(FilterConfiguration configuration) : base(FilterType.CreditCard, configuration) { }

    /// <inheritdoc/>
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, CreditCardAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
