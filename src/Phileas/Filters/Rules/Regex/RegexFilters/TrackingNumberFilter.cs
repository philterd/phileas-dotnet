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
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Rules.Regex.RegexFilters;

/// <summary>
///     Regex-based filter that detects package tracking number entities in plain text.
/// </summary>
public class TrackingNumberFilter : RegexFilter
{
    /// <summary>
    ///     Analyzers are keyed by the carriers enabled and whether spaces are allowed, so each distinct
    ///     configuration compiles once per process rather than once per request.
    /// </summary>
    private static readonly ConcurrentDictionary<(bool Ups, bool Fedex, bool Usps, bool Spaces), Analyzer>
        Analyzers = new();

    private readonly bool _allowSpaces;
    private readonly bool _fedex;
    private readonly bool _ups;
    private readonly bool _usps;

    /// <summary>
    ///     Initializes a new <see cref="TrackingNumberFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    /// <param name="ups">Detect UPS numbers (<c>1Z</c> followed by sixteen characters).</param>
    /// <param name="fedex">Detect FedEx numbers (twelve to fifteen digits).</param>
    /// <param name="usps">Detect USPS numbers (twenty to twenty-two digits).</param>
    /// <param name="allowSpaces">Also detect a number written in space-separated groups.</param>
    public TrackingNumberFilter(FilterConfiguration configuration, bool ups = true, bool fedex = true,
        bool usps = true, bool allowSpaces = false) : base(FilterType.TrackingNumber, configuration)
    {
        _ups = ups;
        _fedex = fedex;
        _usps = usps;
        _allowSpaces = allowSpaces;
    }

    private static Analyzer AnalyzerFor(bool ups, bool fedex, bool usps, bool allowSpaces)
    {
        return Analyzers.GetOrAdd((ups, fedex, usps, allowSpaces), key =>
        {
            var patterns = new List<FilterPattern>();

            // With spaces allowed a separator may sit between any two characters, so a number printed
            // in groups matches whole rather than as its separate runs. The run still has to end on a
            // character, so a trailing space is never part of the span.
            string Run(string character, int min, int max)
            {
                return key.Spaces
                    ? $"{character}(?:[ ]?{character}){{{min - 1},{max - 1}}}"
                    : $"{character}{{{min},{max}}}";
            }

            if (key.Ups)
                patterns.Add(new FilterPattern.Builder()
                    .WithPattern(@"\b1Z" + (key.Spaces ? "[ ]?" : string.Empty) + Run("[0-9A-Z]", 16, 16))
                    .WithInitialConfidence(0.90).Build());

            if (key.Usps)
                patterns.Add(new FilterPattern.Builder().WithPattern(@"\b" + Run("[0-9]", 20, 22))
                    .WithInitialConfidence(0.70).Build());

            if (key.Fedex)
                patterns.Add(new FilterPattern.Builder().WithPattern(@"\b" + Run("[0-9]", 12, 15))
                    .WithInitialConfidence(0.60).Build());

            return new Analyzer(patterns.ToArray());
        });
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, AnalyzerFor(_ups, _fedex, _usps, _allowSpaces), input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}