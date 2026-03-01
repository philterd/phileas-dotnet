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
///     Regex-based filter that detects currency amount entities in plain text.
/// </summary>
public class CurrencyFilter : RegexFilter
{
    private static readonly Analyzer CurrencyAnalyzer = new(
        new FilterPattern.Builder()
            .WithPattern(@"\$\s?[0-9,]+(\.[0-9]{1,2})?(?:\s?(million|billion|trillion|thousand))?",
                RegexOptions.IgnoreCase).WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder().WithPattern(@"\b[0-9,]+(\.[0-9]{1,2})?\s?(USD|EUR|GBP|JPY|CAD|AUD|CHF|CNY)\b")
            .WithInitialConfidence(0.90).Build()
    );

    /// <summary>
    ///     Initializes a new <see cref="CurrencyFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    public CurrencyFilter(FilterConfiguration configuration) : base(FilterType.Currency, configuration)
    {
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, CurrencyAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}