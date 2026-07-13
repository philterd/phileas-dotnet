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
///     Exact (case-insensitive) dictionary filter. Matches whitespace n-grams of the input against a term
///     set, tolerating leading/trailing punctuation on a token. Mirrors the Java <c>SetDictionaryFilter</c>.
/// </summary>
public class SetDictionaryFilter : AbstractDictionaryFilter
{
    private readonly HashSet<string> _lowerCaseTerms = new();
    private int _maxNgramSize;

    /// <summary>Creates a filter whose terms are the built-in dictionary for <paramref name="filterType" />.</summary>
    public SetDictionaryFilter(FilterType filterType, FilterConfiguration configuration)
        : base(filterType, configuration)
    {
        Init(LoadData(filterType).Keys);
    }

    /// <summary>Creates a filter from an explicit term set with an optional classification label.</summary>
    public SetDictionaryFilter(FilterType filterType, FilterConfiguration configuration, IEnumerable<string> terms,
        string? classification)
        : base(filterType, configuration)
    {
        Classification = classification;
        Init(terms);
    }

    private void Init(IEnumerable<string> terms)
    {
        foreach (var term in terms)
        {
            var split = Rx.Split(term, @"\s");
            if (split.Length > _maxNgramSize) _maxNgramSize = split.Length;
            _lowerCaseTerms.Add(term.ToLowerInvariant());
        }
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = new List<Span>();

        foreach (var (position, ngram) in GetNgramsUpToLength(input, _maxNgramSize))
        {
            var begin = 0;
            var end = ngram.Length;
            var matched = _lowerCaseTerms.Contains(ngram.ToLowerInvariant());

            if (!matched)
            {
                while (begin < end && !char.IsLetterOrDigit(ngram[begin])) begin++;
                while (end > begin && !char.IsLetterOrDigit(ngram[end - 1])) end--;
                if (begin != 0 || end != ngram.Length)
                {
                    matched = _lowerCaseTerms.Contains(ngram.Substring(begin, end - begin).ToLowerInvariant());
                }
            }

            if (!matched) continue;

            var characterStart = position.Start + begin;
            var characterEnd = position.Start + end;
            var originalToken = input.Substring(characterStart, characterEnd - characterStart);
            var isIgnored = Ignored.Contains(originalToken);
            const double confidence = 1.0;
            var window = GetWindow(input, characterStart, characterEnd);
            var replacement = GetReplacement(policy, context, originalToken, window, confidence, Classification, null);

            spans.Add(Span.Make(characterStart, characterEnd, FilterType, context, confidence, originalToken,
                replacement.Value, replacement.Salt, isIgnored, replacement.Applied, window, Priority,
                replacement.Color));
        }

        return new Filtered(context, piece, spans);
    }
}
