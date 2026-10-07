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
using System.Text.RegularExpressions;
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
    /// <param name="ups">Detect UPS numbers: <c>1Z</c> and sixteen characters, <c>T</c> and ten digits, or 26 digits.</param>
    /// <param name="fedex">Detect FedEx numbers: 12, 15, 20 or 22 digits.</param>
    /// <param name="usps">
    ///     Detect USPS numbers: 22 or 24 digits starting 92 to 95, 20 digits starting 92 to 95 in five groups
    ///     of four, 16 digits starting 70, 14, 23 or 03, two letters, nine digits and two letters, or 26, 28,
    ///     30 or 34 digits.
    /// </param>
    /// <param name="allowSpaces">Also detect a number written in space-separated groups.</param>
    public TrackingNumberFilter(FilterConfiguration configuration, bool ups = true, bool fedex = true,
        bool usps = true, bool allowSpaces = false) : base(FilterType.TrackingNumber, configuration)
    {
        _ups = ups;
        _fedex = fedex;
        _usps = usps;
        _allowSpaces = allowSpaces;
    }

    // The formats and confidences are the Java filter's, so the ports detect the same numbers. Three
    // differences are deliberate (see philterd/phileas-dotnet#154):
    // - Java's UPS pattern for a bare 9-digit number is left out. It matches any standalone 9-digit
    //   number, including SSNs written without dashes, ZIP+4 codes and invoice numbers.
    // - Java's UPS "T" pattern allows letters after the T, so it matches ordinary 11-letter words such as
    //   "Temperature". Here the ten characters after the T must be digits.
    // - The FedEx and digit-only lengths are exact, as in Java. This port used to accept any length from
    //   12 to 15 and from 20 to 22, so 13, 14 and 21 digits were detected here and not in Java.
    private static Analyzer AnalyzerFor(bool ups, bool fedex, bool usps, bool allowSpaces)
    {
        return Analyzers.GetOrAdd((ups, fedex, usps, allowSpaces), key =>
        {
            var patterns = new List<FilterPattern>();

            // With spaces allowed a single space may sit between any two characters, so a number printed
            // in groups matches whole rather than as its separate runs. The run still has to end on a
            // character, so a trailing space is never part of the span.
            string Run(string character, int length)
            {
                return key.Spaces
                    ? $"{character}(?:[ ]?{character}){{{length - 1}}}"
                    : $"{character}{{{length}}}";
            }

            var sp = key.Spaces ? "[ ]?" : string.Empty;

            // Every pattern starts and ends on a word boundary, so a run that no format matches in full is
            // not matched at all rather than redacted in part. See philterd/phileas-dotnet#156.
            void Add(string pattern, double confidence, string carrier)
            {
                patterns.Add(new FilterPattern.Builder()
                    .WithPattern(@"\b" + pattern + @"\b", RegexOptions.IgnoreCase)
                    .WithInitialConfidence(confidence)
                    .WithClassification(carrier)
                    .Build());
            }

            if (key.Fedex)
            {
                Add(Run("[0-9]", 20), 0.75, "fedex");
                Add(Run("[0-9]", 15), 0.75, "fedex");
                Add(Run("[0-9]", 12), 0.75, "fedex");
                Add(Run("[0-9]", 22), 0.75, "fedex");
            }

            if (key.Ups)
            {
                Add("1Z" + sp + Run("[0-9A-Z]", 16), 0.90, "ups");
                Add("T" + sp + Run("[0-9]", 10), 0.90, "ups");
                Add(Run("[0-9]", 26), 0.75, "ups");
            }

            if (key.Usps)
            {
                Add("(?:94|93|92|95)" + sp + Run("[0-9]", 20), 0.90, "usps");
                Add("(?:94|93|92|95)" + sp + Run("[0-9]", 22), 0.90, "usps");
                // A 20-digit number starting 92 to 95 printed in five groups of four ("9400 1000 0000 0000
                // 0000"), matched whether or not spaces are allowed, as in the Java filter.
                Add("(?:94|93|92|95)[0-9]{2}(?: [0-9]{4}){4}", 0.90, "usps");
                Add("(?:70|14|23|03)" + sp + Run("[0-9]", 14), 0.90, "usps");
                // International: two letters, nine digits, two letters ("EA123456789US").
                Add("[A-Z]{2}" + sp + Run("[0-9]", 9) + sp + "[A-Z]{2}", 0.90, "usps");
                Add(Run("[0-9]", 34), 0.75, "usps");
                Add(Run("[0-9]", 30), 0.75, "usps");
                Add(Run("[0-9]", 28), 0.75, "usps");
                Add(Run("[0-9]", 26), 0.75, "usps");
            }

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