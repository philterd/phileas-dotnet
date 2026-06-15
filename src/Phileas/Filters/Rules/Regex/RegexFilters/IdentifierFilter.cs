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

/// <summary>Detects custom identifiers matching a user-supplied regex. Mirrors the Java <c>IdentifierFilter</c>.</summary>
public class IdentifierFilter : RegexFilter
{
    private readonly Analyzer _analyzer;
    private readonly ISpanValidator? _validator;

    /// <summary>Creates an identifier filter for the given pattern, classification, case sensitivity and capture group.</summary>
    /// <param name="configuration">The filter configuration.</param>
    /// <param name="classification">The classification label applied to matches.</param>
    /// <param name="regex">The user-supplied regular expression.</param>
    /// <param name="caseSensitive">Whether matching is case-sensitive.</param>
    /// <param name="groupNumber">The capture-group number whose match forms the span (0 = whole match).</param>
    /// <param name="validator">
    ///     An optional post-match validator. When supplied, a match is kept only if it passes; when
    ///     <see langword="null" />, every match is kept (behavior unchanged).
    /// </param>
    public IdentifierFilter(FilterConfiguration configuration, string classification, string regex,
        bool caseSensitive, int groupNumber, ISpanValidator? validator = null)
        : base(FilterType.Identifier, configuration)
    {
        _validator = validator;

        var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
        // The pattern is user-supplied, so bound its match time by the configured regex budget: a
        // catastrophic pattern is aborted (yielding no spans) rather than stalling filtering.
        var pattern = new System.Text.RegularExpressions.Regex(regex, options,
            TimeSpan.FromMilliseconds(configuration.RegexTimeoutMs));
        var filterPattern = new FilterPattern.Builder()
            .WithPattern(pattern)
            .WithInitialConfidence(0.90)
            .WithClassification(classification)
            .WithGroupNumber(groupNumber)
            .Build();
        _analyzer = new Analyzer(filterPattern);
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, _analyzer, input, context, piece);

        // With a validator, a match is kept only if it passes, so a generic identifier can reject
        // format-valid but invalid values. With no validator, every match is kept.
        if (_validator != null)
            spans = spans.Where(span => _validator.Validate(span)).ToList();

        return new Filtered(context, piece, spans);
    }
}
