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
///     Regex-based filter that detects passport number entities in plain text.
/// </summary>
public class PassportNumberFilter : RegexFilter
{
    private static readonly Analyzer PassportAnalyzer = new(
        // 1-2 letter prefix + digits: most alphanumeric passports, plus the US passport card (C + 8 digits).
        new FilterPattern.Builder().WithPattern(@"\b[A-Z]{1,2}[0-9]{6,9}\b").WithInitialConfidence(0.75).Build(),
        // All-numeric 9-digit US passport book number (no leading letter). Ambiguous with a bare SSN or
        // driver's-license number, so a lower confidence; it edges out the driver's-license 9-digit shape.
        new FilterPattern.Builder().WithPattern(@"\b[0-9]{9}\b").WithInitialConfidence(0.55).Build()
    );

    /// <summary>
    ///     Initializes a new <see cref="PassportNumberFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    public PassportNumberFilter(FilterConfiguration configuration) : base(FilterType.PassportNumber, configuration)
    {
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, PassportAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}