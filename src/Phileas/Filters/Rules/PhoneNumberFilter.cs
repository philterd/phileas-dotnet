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
// Alias the policy model: the libphonenumber namespace PhoneNumbers declares its own PhoneNumber type.
using PolicyPhoneNumber = Phileas.Policy.Filters.PhoneNumber;
// Alias the BCL type: the sibling namespace Phileas.Filters.Rules.Regex otherwise shadows the name `Regex`.
using SysRegex = System.Text.RegularExpressions.Regex;

namespace Phileas.Filters.Rules;

/// <summary>
///     Detects phone numbers with Google's libphonenumber (the maintained <c>libphonenumber-csharp</c> port),
///     scanning text with <see cref="PhoneNumberUtil.FindNumbers(string, string, PhoneNumberUtil.Leniency, long)" />.
///     Text is scanned once per configured region (the policy's <c>region</c> property, <c>US</c> by default) with
///     <see cref="PhoneNumberUtil.Leniency.POSSIBLE" />, and the results are merged and de-duplicated. A
///     <c>+</c>-prefixed international number is found regardless of region, matching the Java Phileas phone filter.
///     This is a scanner, not a regex filter: it extends <see cref="RulesFilter" /> directly.
/// </summary>
public class PhoneNumberFilter : RulesFilter
{
    // A fully NANP-formatted number (optional country code, 3-3-4 grouping). A found number that matches is
    // scored highest; other found numbers are scored by length, mirroring the Java PhoneNumberRulesFilter.
    private static readonly SysRegex NanpPattern =
        new(@"^(\+\d{1,2}\s)?\(?\d{3}\)?[\s.-]\d{3}[\s.-]\d{4}$", RegexOptions.Compiled);

    private static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();

    private readonly List<string> _regions;

    /// <summary>
    ///     Initializes a new <see cref="PhoneNumberFilter" /> with the given configuration and the default
    ///     region (<see cref="PolicyPhoneNumber.DefaultRegion" />).
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    public PhoneNumberFilter(FilterConfiguration configuration) : this(configuration, null)
    {
    }

    /// <summary>Initializes a new <see cref="PhoneNumberFilter" /> with the given configuration and regions.</summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    /// <param name="regions">
    ///     The ISO 3166-1 alpha-2 region(s) used to interpret numbers written without a <c>+</c> country code.
    ///     <see langword="null" /> or empty falls back to <see cref="PolicyPhoneNumber.DefaultRegion" />.
    /// </param>
    public PhoneNumberFilter(FilterConfiguration configuration, IEnumerable<string>? regions)
        : base(FilterType.PhoneNumber, configuration)
    {
        var configured = regions?.Where(region => !string.IsNullOrWhiteSpace(region)).ToList();
        _regions = configured is { Count: > 0 }
            ? configured
            : new List<string> { PolicyPhoneNumber.DefaultRegion };
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = new List<Span>();

        if (!policy.Identifiers.HasFilter(FilterType))
            return new Filtered(context, piece, spans);

        // Scan once per configured region and merge the results. A "+"-prefixed number is found under every
        // region and a bare national-format number may match in several, so overlapping matches are de-duplicated.
        var matches = new List<PhoneNumberMatch>();
        foreach (var region in _regions)
            matches.AddRange(PhoneUtil.FindNumbers(input, region, PhoneNumberUtil.Leniency.POSSIBLE, long.MaxValue));

        foreach (var match in Dedupe(matches))
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

    /// <summary>
    ///     Merges the matches found across the configured regions, removing overlapping ones. When two matches
    ///     overlap the better one is kept: a valid number beats a merely-possible one, and among equally-valid
    ///     matches the longer span wins. Mirrors the Java <c>PhoneNumberRulesFilter</c>.
    /// </summary>
    private static List<PhoneNumberMatch> Dedupe(List<PhoneNumberMatch> matches)
    {
        // Best-first ordering so a greedy sweep keeps the strongest match in each overlapping cluster.
        var sorted = new List<PhoneNumberMatch>(matches);
        sorted.Sort((a, b) =>
        {
            var aValid = PhoneUtil.IsValidNumber(a.Number);
            var bValid = PhoneUtil.IsValidNumber(b.Number);
            if (aValid != bValid) return aValid ? -1 : 1;
            if (a.Length != b.Length) return b.Length.CompareTo(a.Length);
            return a.Start.CompareTo(b.Start);
        });

        var kept = new List<PhoneNumberMatch>();
        foreach (var candidate in sorted)
        {
            var overlaps = kept.Any(accepted =>
                candidate.Start < accepted.Start + accepted.Length &&
                accepted.Start < candidate.Start + candidate.Length);

            if (!overlaps) kept.Add(candidate);
        }

        // Restore document order so emitted spans are left-to-right.
        kept.Sort((a, b) => a.Start.CompareTo(b.Start));

        return kept;
    }
}
