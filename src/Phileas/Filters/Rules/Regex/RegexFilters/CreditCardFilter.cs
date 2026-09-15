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

using System.Collections.Concurrent;
using Phileas.Model;
using Phileas.Services.Validators;
using Rx = System.Text.RegularExpressions.Regex;
using RegexOptions = System.Text.RegularExpressions.RegexOptions;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Rules.Regex.RegexFilters;

/// <summary>
///     Regex-based filter that detects credit card number entities in plain text.
///     <para>
///         Detection happens in two stages. The pattern matches any run of 13 to 16 digits, separators
///         allowed between them, and the issuer prefix is checked afterwards along with the Luhn
///         checksum as part of <c>onlyValidCreditCardNumbers</c>. Keeping issuer prefixes out of the
///         pattern is deliberate: when they were in it, a card in a range the pattern did not list went
///         undetected rather than merely unvalidated, which is the worse way for a redaction filter to
///         fail. Mastercard's 2221-2720 range had been missing since it opened in 2017.
///     </para>
///     <para>
///         The consequence is that <c>onlyValidCreditCardNumbers</c> carries more weight than its name
///         suggests. With it on, which is the default, only issuer-shaped numbers passing the checksum
///         are kept. With it off, every run of 13 to 16 digits is, which is when
///         <c>ignoreWhenInUnixTimestamp</c> becomes worth enabling.
///     </para>
/// </summary>
public class CreditCardFilter : RegexFilter
{
    /// <summary>
    ///     Any run of 13 to 16 ASCII digits, separators allowed between them. The digit class is
    ///     spelled out because .NET's <c>\d</c> spans every Unicode decimal digit, which would let a
    ///     fullwidth-digit run be redacted as a card whenever validation is switched off. Detection is deliberately
    ///     brand-agnostic: encoding brand prefixes here made the filter go stale whenever a network
    ///     opened a range, and it did. Brand knowledge now sits in <see cref="IsKnownBrand" />, where
    ///     being out of date costs precision rather than letting a card through unredacted.
    /// </summary>
    private const string CardShape = @"(?:[0-9][ -]*?){13,16}";

    /// <summary>
    ///     The issuer prefixes, applied after the separators are stripped. Mastercard's 2-series
    ///     (2221-2720, opened in 2017) is included: omitting it is what let valid cards through.
    /// </summary>
    private static readonly Rx BrandedNumber = new(
        @"^(?:4[0-9]{12}(?:[0-9]{3})?"                                        // Visa
        + @"|(?:5[1-5][0-9]{2}|222[1-9]|22[3-9][0-9]|2[3-6][0-9]{2}|27[01][0-9]|2720)[0-9]{12}" // Mastercard
        + @"|3[47][0-9]{13}"                                                  // American Express
        + @"|3(?:0[0-5]|[68][0-9])[0-9]{11}"                                  // Diners Club
        + @"|6(?:011|5[0-9]{2})[0-9]{12}"                                     // Discover
        + @"|62[0-9]{14}"                                                     // UnionPay, 16-digit
        + @"|(?:2131|1800|35[0-9]{3})[0-9]{11})$",                            // JCB
        RegexOptions.None, RegexDefaults.MatchTimeout);

    /// <summary>
    ///     A thirteen-digit epoch millisecond count. Under a brand-agnostic pattern such a run is a
    ///     candidate, which is what <c>ignoreWhenInUnixTimestamp</c> exists to suppress.
    /// </summary>
    private static readonly Rx UnixTimestamp = new(@"^1[5-8][0-9]{11}$", RegexOptions.None,
        RegexDefaults.MatchTimeout);

    /// <summary>
    ///     Analyzers are keyed by the options that change the pattern, so each distinct configuration
    ///     compiles once per process rather than once per request.
    /// </summary>
    private static readonly ConcurrentDictionary<bool, Analyzer> Analyzers = new();

    private readonly bool _ignoreWhenInUnixTimestamp;
    private readonly bool _onlyValidCreditCardNumbers;
    private readonly bool _onlyWordBoundaries;

    /// <summary>
    ///     Initializes a new <see cref="CreditCardFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    /// <param name="onlyValidCreditCardNumbers">Keep only issuer-shaped numbers that pass Luhn.</param>
    /// <param name="onlyWordBoundaries">Require the number to sit on a word boundary.</param>
    /// <param name="ignoreWhenInUnixTimestamp">Drop digit runs that look like a Unix timestamp.</param>
    public CreditCardFilter(FilterConfiguration configuration, bool onlyValidCreditCardNumbers = true,
        bool onlyWordBoundaries = true, bool ignoreWhenInUnixTimestamp = false)
        : base(FilterType.CreditCard, configuration)
    {
        _onlyValidCreditCardNumbers = onlyValidCreditCardNumbers;
        _onlyWordBoundaries = onlyWordBoundaries;
        _ignoreWhenInUnixTimestamp = ignoreWhenInUnixTimestamp;
    }

    /// <summary>Whether <paramref name="text" /> is shaped like a Unix timestamp in epoch milliseconds.</summary>
    internal static bool IsUnixTimestamp(string text)
    {
        return UnixTimestamp.IsMatch(Digits(text));
    }

    /// <summary>Whether the digits of <paramref name="text" /> carry a known issuer prefix.</summary>
    internal static bool IsKnownBrand(string text)
    {
        return BrandedNumber.IsMatch(Digits(text));
    }

    private static string Digits(string text)
    {
        return new string(text.Where(char.IsAsciiDigit).ToArray());
    }

    private static Analyzer AnalyzerFor(bool onlyWordBoundaries)
    {
        return Analyzers.GetOrAdd(onlyWordBoundaries, wordBoundaries =>
        {
            // Without the boundaries a number embedded in a longer token is still found, at the cost
            // of precision, so it carries a lower initial confidence. The lookahead form lets
            // overlapping candidates be found, matching the Java filter.
            return wordBoundaries
                ? new Analyzer(new FilterPattern.Builder()
                    .WithPattern(@"\b" + CardShape + @"\b").WithInitialConfidence(0.90).Build())
                : new Analyzer(new FilterPattern.Builder()
                    .WithPattern("(?=(" + CardShape + "))").WithGroupNumber(1)
                    .WithInitialConfidence(0.70).Build());
        });
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, AnalyzerFor(_onlyWordBoundaries), input, context, piece);
        spans = PostFilter(spans, input);

        // Ordered as in the Java filter: the timestamp guard runs first, so it still applies when
        // validation is switched off, which is exactly when the shape pattern is at its noisiest.
        if (_ignoreWhenInUnixTimestamp)
            spans = spans.Where(span => !IsUnixTimestamp(span.Text)).ToList();

        // Validation is both halves: a known issuer prefix and the Luhn checksum. Luhn alone would
        // admit roughly one in ten arbitrary digit runs.
        if (_onlyValidCreditCardNumbers)
            spans = spans.Where(span => IsKnownBrand(span.Text) && LuhnValidator.IsValid(span.Text))
                .ToList();

        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}