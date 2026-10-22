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
///     Text is scanned once per configured region (the policy's <c>region</c> property, <c>US</c> by default) at the
///     configured leniency (the policy's <c>leniency</c> property, <see cref="PhoneNumberUtil.Leniency.POSSIBLE" /> by
///     default), and the results are merged and de-duplicated. A
///     <c>+</c>-prefixed international number is found regardless of region, matching the Java Phileas phone filter.
///     This is a scanner, not a regex filter: it extends <see cref="RulesFilter" /> directly.
/// </summary>
public class PhoneNumberFilter : RulesFilter
{
    // A fully NANP-formatted number (optional country code, 3-3-4 grouping). A found number that matches is
    // scored highest; other found numbers are scored by length, mirroring the Java PhoneNumberRulesFilter.
    /// <summary>Words near a match that raise its confidence; the Java port's list.</summary>
    private static readonly HashSet<string> ContextualTerms = new() { "phone", "telephone", "fax", "cell", "mobile" };

    private static readonly SysRegex NanpPattern =
        new(@"^(\+\d{1,2}\s)?\(?\d{3}\)?[\s.-]\d{3}[\s.-]\d{4}$", RegexOptions.Compiled,
            RegexDefaults.MatchTimeout);

    // A bare "1" country code followed by a phone-formatted number punctuated with the same separator throughout,
    // such as "1-112-997-5633". Without a "+", libphonenumber strips the "1" as the country code and then an area
    // code's leading "1" as a national prefix, leaving too few digits for a possible number, so the match is
    // dropped (#176). A number punctuated differently from the "1", as in "Item 1 112-997-5633", is not rewritten.
    private const string BareCountryCode = @"(?<![0-9+])1([-. ])(?=\(?[0-9]{3}\)?\1[0-9]{3}\1[0-9]{4})";

    private static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();

    private readonly PhoneNumberUtil.Leniency _leniency;

    // The first configured region that uses country code 1, or null when none does.
    private readonly string? _northAmericanRegion;

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
        : this(configuration, regions, PhoneNumberUtil.Leniency.POSSIBLE)
    {
    }

    /// <summary>
    ///     Initializes a new <see cref="PhoneNumberFilter" /> with the given configuration, regions and leniency.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    /// <param name="regions">
    ///     The ISO 3166-1 alpha-2 region(s) used to interpret numbers written without a <c>+</c> country code.
    ///     <see langword="null" /> or empty falls back to <see cref="PolicyPhoneNumber.DefaultRegion" />.
    /// </param>
    /// <param name="leniency">The libphonenumber leniency every scan uses.</param>
    public PhoneNumberFilter(FilterConfiguration configuration, IEnumerable<string>? regions,
        PhoneNumberUtil.Leniency leniency)
        : base(FilterType.PhoneNumber, configuration)
    {
        _leniency = leniency;
        var configured = regions?.Where(region => !string.IsNullOrWhiteSpace(region)).ToList();
        _regions = configured is { Count: > 0 }
            ? configured
            : new List<string> { PolicyPhoneNumber.DefaultRegion };
        _northAmericanRegion = _regions.FirstOrDefault(region => PhoneUtil.GetCountryCodeForRegion(region) == 1);
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
            matches.AddRange(PhoneUtil.FindNumbers(input, region, _leniency, long.MaxValue));

        // Also scan, once, a copy in which a bare "1" country code becomes "+1", so "1-112-997-5633" is found as
        // "+1 112-997-5633" is. "1-" and "+1" are the same length, so offsets in the copy are offsets in the input.
        // Only when a configured region uses country code 1, so a policy for another region does not read these as
        // North American numbers. Java keeps a copy's match only where the input had none, but this libphonenumber
        // port finds "112 997 5633" in "1 112 997 5633", which Java does not, so here the copy's match replaces it.
        if (_northAmericanRegion != null)
            AddBareCountryCodeMatches(input, matches);

        foreach (var match in Dedupe(matches))
        {
            // From the input rather than RawString, which is the rewritten text for a match found in the copy.
            var text = input.Substring(match.Start, match.Length);
            var start = match.Start;
            var end = match.Start + match.Length;

            // Confidence mirrors the Java filter: a cleanly NANP-formatted match scores highest; other found
            // numbers (e.g. international formats) score by length.
            var confidence = ApplyContextualTerms(ContextualTerms,
                NanpPattern.IsMatch(text) ? 0.95 : text.Length > 14 ? 0.75 : 0.60, input, start, end);

            var window = GetWindow(input, start, end);
            var replacement = GetReplacement(policy, context, text, window, confidence, Classification, null);
            if (replacement == null) continue; // no strategy's condition was satisfied: leave it

            spans.Add(Span.Make(start, end, FilterType, context, confidence, text,
                replacement.Value, replacement.Salt, IsIgnored(text), replacement.Applied, window, Priority,
                replacement.Color));
        }

        var filtered = PostFilter(spans, input);
        filtered = Span.DropOverlappingSpans(filtered);
        return new Filtered(context, piece, filtered);
    }

    /// <summary>
    ///     Scans a copy of <paramref name="input" /> in which each bare <c>1</c> country code is written <c>+1</c>,
    ///     and adds the numbers found there that include a rewritten <c>1</c>. Matches elsewhere in the copy are the
    ///     input's own and are left alone. A timeout is recorded rather than thrown, so a number this scan would have
    ///     found is reported as unsearched instead of silently missed.
    /// </summary>
    private void AddBareCountryCodeMatches(string input, List<PhoneNumberMatch> matches)
    {
        List<int> rewrites;
        try
        {
            rewrites = SysRegex.Matches(input, BareCountryCode, RegexOptions.None, RegexTimeout)
                .Select(match => match.Index).ToList();
        }
        catch (RegexMatchTimeoutException)
        {
            RecordRegexTimeout($"{FilterType} pattern '{BareCountryCode}'");
            return;
        }

        if (rewrites.Count == 0) return;

        var copy = input.ToCharArray();
        foreach (var index in rewrites)
        {
            copy[index] = '+';
            copy[index + 1] = '1';
        }

        foreach (var match in PhoneUtil.FindNumbers(new string(copy), _northAmericanRegion, _leniency, long.MaxValue))
        {
            var end = match.Start + match.Length;
            if (!rewrites.Any(index => match.Start <= index && index < end)) continue;

            // Replace the input's matches inside this one, so the "1" joins the span where the input found only the
            // rest ("112 997 5633" in "1 112 997 5633"); drop this one where it only partly overlaps a match.
            var overlapping = matches.Where(m => match.Start < m.Start + m.Length && m.Start < end).ToList();
            if (overlapping.Any(m => m.Start < match.Start || m.Start + m.Length > end)) continue;

            matches.RemoveAll(overlapping.Contains);
            matches.Add(match);
        }
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
