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

namespace Phileas.Filters.Regex;

/// <summary>
/// Regex-based filter that detects package tracking number entities in plain text.
/// </summary>
public class TrackingNumberFilter : RegexFilter
{
    private static readonly Analyzer TrackingAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b1Z[0-9A-Z]{16}\b").WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder().WithPattern(@"\b[0-9]{20,22}\b").WithInitialConfidence(0.70).Build(),
        new FilterPattern.Builder().WithPattern(@"\b[0-9]{12,15}\b").WithInitialConfidence(0.60).Build()
    );

    /// <summary>
    /// Initializes a new <see cref="TrackingNumberFilter"/> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    public TrackingNumberFilter(FilterConfiguration configuration) : base(FilterType.TrackingNumber, configuration) { }

    /// <inheritdoc/>
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, TrackingAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
