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
using PhoneNumbers;
using PhileasPolicy = Phileas.Policy.Policy;
// Alias the BCL type: the sibling namespace Phileas.Filters.Rules.Regex otherwise shadows the name `Regex`.
using SysRegex = System.Text.RegularExpressions.Regex;

namespace Phileas.Filters.Rules;

/// <summary>
///     Detects phone numbers with Google's libphonenumber (the maintained <c>libphonenumber-csharp</c> port),
///     scanning text with <see cref="PhoneNumberUtil.FindNumbers(string, string, PhoneNumberUtil.Leniency, long)" />.
///     A default region of <c>US</c> and <see cref="PhoneNumberUtil.Leniency.POSSIBLE" /> find NANP numbers and
///     any <c>+</c>-prefixed international number regardless of region, matching the Java Phileas phone filter.
///     This is a scanner, not a regex filter: it extends <see cref="RulesFilter" /> directly.
/// </summary>
public class PhoneNumberFilter : RulesFilter
{
    // A fully NANP-formatted number (optional country code, 3-3-4 grouping). A found number that matches is
    // scored highest; other found numbers are scored by length, mirroring the Java PhoneNumberRulesFilter.
    private static readonly SysRegex NanpPattern =
        new(@"^(\+\d{1,2}\s)?\(?\d{3}\)?[\s.-]\d{3}[\s.-]\d{4}$", RegexOptions.Compiled);

    private static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();

    /// <summary>Initializes a new <see cref="PhoneNumberFilter" /> with the given configuration.</summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    public PhoneNumberFilter(FilterConfiguration configuration) : base(FilterType.PhoneNumber, configuration)
    {
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = new List<Span>();

        if (!policy.Identifiers.HasFilter(FilterType))
            return new Filtered(context, piece, spans);

        foreach (var match in PhoneUtil.FindNumbers(input, "US", PhoneNumberUtil.Leniency.POSSIBLE, long.MaxValue))
        {
            var text = match.RawString;
            var start = match.Start;
            var end = match.Start + match.Length;

            // Confidence mirrors the Java filter: a cleanly NANP-formatted match scores highest; other found
            // numbers (e.g. international formats) score by length.
            var confidence = NanpPattern.IsMatch(text) ? 0.95 : text.Length > 14 ? 0.75 : 0.60;

            var window = GetWindow(input, start, end);
            var replacement = GetReplacement(policy, context, text, window, confidence, Classification, null);

            spans.Add(Span.Make(start, end, FilterType, context, confidence, text,
                replacement.Value, replacement.Salt, IsIgnored(text), replacement.Applied, window, Priority,
                replacement.Color));
        }

        var filtered = PostFilter(spans, input);
        filtered = Span.DropOverlappingSpans(filtered);
        return new Filtered(context, piece, filtered);
    }
}
