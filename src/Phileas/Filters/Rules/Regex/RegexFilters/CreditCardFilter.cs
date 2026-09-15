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
/// </summary>
public class CreditCardFilter : RegexFilter
{
    private const string BrandedNumber =
        @"(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|3[47][0-9]{13}|6(?:011|5[0-9]{2})[0-9]{12}|3(?:0[0-5]|[68][0-9])[0-9]{11}|(?:2131|1800|35\d{3})\d{11})";

    private const string GroupedNumber = @"\d{4}[\s\-]\d{4}[\s\-]\d{4}[\s\-]\d{4}";

    /// <summary>
    ///     A ten-digit epoch second count in the 2017-2027 range, written with the milliseconds that
    ///     make it sixteen digits long and so indistinguishable from a card number by shape alone.
    /// </summary>
    private static readonly Rx UnixTimestamp = new(@"^1[5-8][0-9]{11}$", RegexOptions.None,
        RegexDefaults.MatchTimeout);

    /// <summary>
    ///     Whether <paramref name="text" /> is shaped like a Unix timestamp in epoch milliseconds.
    ///     Exposed for testing: the guard cannot currently change what this filter returns, because the
    ///     brand patterns never match a timestamp, so its own correctness is not otherwise observable.
    /// </summary>
    internal static bool IsUnixTimestamp(string text)
    {
        return UnixTimestamp.IsMatch(text);
    }

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
    /// <param name="onlyValidCreditCardNumbers">Keep only numbers that pass the Luhn checksum.</param>
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

    private static Analyzer AnalyzerFor(bool onlyWordBoundaries)
    {
        return Analyzers.GetOrAdd(onlyWordBoundaries, wordBoundaries =>
        {
            // Without the boundaries a card number embedded in a longer run of digits or letters is
            // still found, at the cost of more false positives, which is why it is not the default.
            var boundary = wordBoundaries ? @"\b" : string.Empty;
            return new Analyzer(
                new FilterPattern.Builder().WithPattern(boundary + BrandedNumber + boundary)
                    .WithInitialConfidence(0.95).Build(),
                new FilterPattern.Builder().WithPattern(boundary + GroupedNumber + boundary)
                    .WithInitialConfidence(0.85).Build());
        });
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, AnalyzerFor(_onlyWordBoundaries), input, context, piece);
        spans = PostFilter(spans, input);

        if (_ignoreWhenInUnixTimestamp)
            spans = spans.Where(span => !IsUnixTimestamp(span.Text)).ToList();

        // The checksum runs over the digits, so a number written in groups is validated as written.
        if (_onlyValidCreditCardNumbers)
            spans = spans.Where(span => LuhnValidator.IsValid(span.Text)).ToList();

        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}