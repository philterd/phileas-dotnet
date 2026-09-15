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
///     Regex-based filter that detects age expression entities in plain text.
/// </summary>
public class AgeFilter : RegexFilter
{
    /// <summary>
    ///     The optional separator between an <c>age</c> or <c>aged</c> keyword and its value, so
    ///     <c>Age: 47</c>, <c>Age = 47</c>, <c>Age - 47</c> and <c>Age:47</c> are read as well as
    ///     <c>age 47</c>. The whitespace sits inside the optional group so a run of it has only one
    ///     possible match; the ambiguous <c>\s*[:=-]?\s*</c> backtracks quadratically.
    /// </summary>
    private const string KeywordSeparator = @"\s*(?:[:=-]\s*)?";

    /// <summary>
    ///     A plausible age written after a keyword: 0 to 125, with an optional fractional part.
    ///     <para>
    ///         The keyword pattern previously took any number at all, so <c>Bronze Age 1200</c> and
    ///         <c>form AGE 2024</c> were redacted as ages. The ceiling is set well above a typical
    ///         maximum lifespan rather than at it, since the cost of the bound is recall on a genuine
    ///         but extreme age.
    ///     </para>
    ///     <para>
    ///         Leading zeros are allowed, so the zero-padded value of a fixed-width form export such as
    ///         <c>AGE 047</c> still reads. The count is bounded rather than written <c>0*</c>, which
    ///         would backtrack over a long run of zeros.
    ///     </para>
    ///     <para>
    ///         Only the keyword pattern is bounded. The <c>years old</c> and <c>y/o</c> forms carry
    ///         their own unit, which is what makes them ages, so a number in front of one needs no
    ///         plausibility check.
    ///     </para>
    /// </summary>
    private const string PlausibleAge = @"0{0,2}(?:1[01][0-9]|12[0-5]|[1-9][0-9]|[0-9])(?:\.[0-9]+)?";

    private static readonly Analyzer AgeAnalyzer = new(
        new FilterPattern.Builder()
            .WithPattern(@"\b[0-9.]+[\s]*(year|years|yrs|yr|yo)(\.?)(\s)*(old)?\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder()
            .WithPattern($@"\b(age)(d)?{KeywordSeparator}{PlausibleAge}\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder()
            .WithPattern(@"\b[0-9.]+[-]*(year|years|yrs|yr|yo)(\.?)(-)*(old)?\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder().WithPattern(@"\b([0-9]{1,3}) (y\/o)\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).Build()
    );

    /// <summary>
    ///     Initializes a new <see cref="AgeFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    public AgeFilter(FilterConfiguration configuration) : base(FilterType.Age, configuration)
    {
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, AgeAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}