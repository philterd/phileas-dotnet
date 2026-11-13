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
    ///     The optional separator between an age keyword and its value, so
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
    private const string PlausibleAge = @"(?:0{0,2}(?:1[01][0-9]|12[0-5]|[1-9][0-9]|[0-9])(?:\.[0-9]+)?)";

    // The composite patterns below are grouped so each can be quantified or alternated as a unit.
    // Their joiners never succeed on the same text (a comma joiner is followed by a digit, a word
    // joiner by "and", "or" or "to"), so a long list that fails to match has one parse to undo and
    // cannot backtrack exponentially.

    /// <summary>Joins two ages with a word or a hyphen: <c>-</c>, <c>to</c>, <c>and</c>, <c>or</c>.</summary>
    private const string AgeWordJoin = @"(?:\s*-\s*|,?\s+(?:and|or|to)\s+)";

    /// <summary>
    ///     After the singular <c>age</c>: one age, or a range, <c>18-65</c> or <c>18 to 65</c>. A
    ///     comma or <c>and</c> is not followed, so <c>age 45, 3 visits</c> is one age.
    /// </summary>
    private const string AgeOrRange = $@"(?:{PlausibleAge}(?:(?:\s*-\s*|\s+to\s+){PlausibleAge})?)";

    /// <summary>
    ///     After <c>aged</c>, <c>between</c> or <c>from</c>: ages joined by words or hyphens, where a
    ///     comma counts only inside a list a word or hyphen closes, so <c>aged 3, 5 and 7</c> and
    ///     <c>aged 39 and 41 and 44</c> are read whole and <c>aged 39, 2 children</c> is one age.
    /// </summary>
    private const string AgeRun =
        $@"(?:{PlausibleAge}(?:(?:\s*,\s*{PlausibleAge})*{AgeWordJoin}{PlausibleAge})*)";

    /// <summary>
    ///     After the plural <c>ages</c>: ages joined by commas, words or hyphens. The plural is what
    ///     marks a list, so a bare comma joins here, as in <c>Ages: 39, 41</c>.
    /// </summary>
    private const string AgeSeries = $@"(?:{PlausibleAge}(?:(?:\s*,\s*|{AgeWordJoin}){PlausibleAge})*)";

    /// <summary>Words near a match that raise its confidence; the Java port's list.</summary>
    private static readonly HashSet<string> ContextualTerms = new() { "age", "years" };

    private static readonly Analyzer AgeAnalyzer = new(ContextualTerms,
        new FilterPattern.Builder()
            .WithPattern(@"\b[0-9.]+[\s]*(year|years|yrs|yr|yo)(\.?)(\s)*(old)?\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).Build(),
        // "age 25", "Age: 47", "at the age of 39", "age 18-65".
        new FilterPattern.Builder()
            .WithPattern($@"\bage(?:\s+of)?{KeywordSeparator}{AgeOrRange}\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).Build(),
        // "aged 65", "aged 39 to 41", "aged 3, 5 and 7".
        new FilterPattern.Builder()
            .WithPattern($@"\baged{KeywordSeparator}{AgeRun}\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).Build(),
        // "ages 39-41", "Ages: 39, 41", "the ages of 39, 41, and 44".
        new FilterPattern.Builder()
            .WithPattern($@"\bages(?:\s+of)?{KeywordSeparator}{AgeSeries}\b", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.90).Build(),
        // "age between 18 and 65", "Age: between 18 and 65", "the ages of between 18 and 65". The
        // keyword is separated from "between" or "from" by whitespace or a separator, never by
        // nothing, so "agebetween" is not a keyword.
        new FilterPattern.Builder()
            .WithPattern($@"\bage[ds]?(?:\s+of)?(?:\s*[:=-]\s*|\s+)(?:between|from)\s+{AgeRun}\b", RegexOptions.IgnoreCase)
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