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
using Rx = System.Text.RegularExpressions.Regex;
using Phileas.Model;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Rules.Dictionary;

/// <summary>
///     Fuzzy dictionary filter. Reports exact matches and, when the sensitivity level allows, near matches
///     within a Levenshtein distance (HIGH: exact, MEDIUM: &lt;=1, LOW: &lt;=2). Mirrors the Java
///     <c>FuzzyDictionaryFilter</c>.
/// </summary>
public class FuzzyDictionaryFilter : AbstractDictionaryFilter
{
    private readonly SensitivityLevel _sensitivityLevel;
    private readonly Dictionary<string, Rx> _dictionary;
    private readonly int _maxNgrams;
    private readonly bool _requireCapitalization;

    /// <summary>Creates a fuzzy filter over the built-in dictionary for <paramref name="filterType" />.</summary>
    public FuzzyDictionaryFilter(FilterType filterType, FilterConfiguration configuration,
        SensitivityLevel sensitivityLevel, bool requireCapitalization)
        : base(filterType, configuration)
    {
        _sensitivityLevel = sensitivityLevel;
        _dictionary = LoadData(filterType);
        _maxNgrams = GetMaxNgrams();
        _requireCapitalization = requireCapitalization;
    }

    /// <summary>Creates a fuzzy filter over an explicit term set.</summary>
    public FuzzyDictionaryFilter(FilterType filterType, FilterConfiguration configuration,
        SensitivityLevel sensitivityLevel, IEnumerable<string> terms, bool requireCapitalization)
        : base(filterType, configuration)
    {
        _sensitivityLevel = sensitivityLevel;
        _dictionary = LoadData(terms);
        _maxNgrams = GetMaxNgrams();
        _requireCapitalization = requireCapitalization;
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = new List<Span>();

        if (policy.Identifiers.HasFilter(FilterType))
        {
            var ngramsByLength = new Dictionary<int, List<(Position Position, string Ngram)>>();
            for (var x = 1; x <= _maxNgrams; x++) ngramsByLength[x] = GetNgramsOfLength(input, x);

            foreach (var (entry, pattern) in _dictionary)
            {
                var match = pattern.Match(input);
                if (match.Success)
                {
                    var startPosition = match.Index;
                    if (!_requireCapitalization || char.IsUpper(input[startPosition]))
                    {
                        spans.Add(CreateSpan(input, startPosition, startPosition + entry.Length, 1.0, context, piece,
                            entry, policy));
                    }
                }
                else if (_sensitivityLevel != SensitivityLevel.Off)
                {
                    var wordsInEntry = entry.Split(' ').Length;
                    if (!ngramsByLength.TryGetValue(wordsInEntry, out var ngrams)) continue;

                    foreach (var (position, ngram) in ngrams)
                    {
                        if (ngram.Length <= 2) continue;
                        if (_requireCapitalization && !char.IsUpper(ngram[0])) continue;

                        var distance = Levenshtein.Distance(entry.ToLowerInvariant(), ngram.ToLowerInvariant());
                        if (_sensitivityLevel == SensitivityLevel.High && distance == 0)
                        {
                            spans.Add(CreateSpan(input, position.Start, position.End, 0.9, context, piece, ngram, policy));
                        }
                        else if (_sensitivityLevel == SensitivityLevel.Medium && distance <= 1)
                        {
                            spans.Add(CreateSpan(input, position.Start, position.End, 0.7, context, piece, ngram, policy));
                        }
                        else if (_sensitivityLevel == SensitivityLevel.Low && distance <= 2)
                        {
                            spans.Add(CreateSpan(input, position.Start, position.End, 0.5, context, piece, ngram, policy));
                        }
                    }
                }
            }
        }

        return new Filtered(context, piece, spans);
    }

    private Span CreateSpan(string text, int characterStart, int characterEnd, double confidence, string context,
        int piece, string token, PhileasPolicy policy)
    {
        var ignored = IsIgnored(text);
        var window = GetWindow(text, characterStart, characterEnd);
        var replacement = GetReplacement(policy, context, token, window, confidence, Classification, null);
        return Span.Make(characterStart, characterEnd, FilterType, context, confidence, token, replacement.Value,
            replacement.Salt, ignored, replacement.Applied, window, Priority);
    }

    private int GetMaxNgrams()
    {
        var max = 0;
        foreach (var key in _dictionary.Keys)
        {
            var n = key.Split(' ').Length;
            if (n > max) max = n;
            if (n >= 20) break;
        }

        return max;
    }
}
