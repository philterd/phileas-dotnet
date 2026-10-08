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

using Phileas.Filters.PostFilters;
using Phileas.Model;

namespace Phileas.Filters.Rules;

/// <summary>
///     Intermediate base class between <see cref="AbstractFilter" /> and concrete rule-based filters.
///     Provides the <see cref="PostFilter" /> hook that concrete filters can override to refine
///     detected spans after the initial matching pass.
/// </summary>
public abstract class RulesFilter : AbstractFilter
{
    /// <summary>
    ///     Initializes the rules filter with the given type and configuration.
    /// </summary>
    /// <param name="filterType">The entity type handled by this filter.</param>
    /// <param name="configuration">Runtime filter configuration.</param>
    protected RulesFilter(FilterType filterType, FilterConfiguration configuration)
        : base(filterType, configuration)
    {
    }

    /// <summary>The confidence added when a contextual term appears near a match. The result is capped at 1.0.</summary>
    public const double ContextualTermBoost = 0.05;

    /// <summary>
    ///     Returns <paramref name="confidence" /> raised by <see cref="ContextualTermBoost" /> when any of
    ///     <paramref name="terms" /> appears within <see cref="AbstractFilter.WindowSize" /> words before or after the
    ///     match, capped at 1.0. Words are split on any whitespace and never include the match itself. A word matches
    ///     a term case-insensitively once punctuation at either end is trimmed from both, so <c>SIN:</c> matches
    ///     <c>sin</c>. A term of several words matches the same words in a row on one side of the match. The boost is
    ///     applied once, however many terms appear. A word longer than 64 characters ends the search on its side.
    /// </summary>
    protected double ApplyContextualTerms(ISet<string>? terms, double confidence, string input, int start, int end)
    {
        if (terms == null || terms.Count == 0 || WindowSize <= 0)
            return confidence;

        var before = WordsBefore(input, start, WindowSize);
        var after = WordsAfter(input, end, WindowSize);

        foreach (var term in terms)
        {
            var phrase = term.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeContextWord).Where(word => word.Length > 0).ToArray();
            if (phrase.Length > 0 && (ContainsPhrase(before, phrase) || ContainsPhrase(after, phrase)))
                // Rounded so the sum is the decimal it reads as: 0.90 + 0.05 is otherwise 0.9500000000000001,
                // which would outrank a 0.95 span and fail a "confidence == 0.95" condition.
                return Math.Min(1.0, Math.Round(confidence + ContextualTermBoost, 6));
        }

        return confidence;
    }

    // No term is anywhere near this long. A longer word ends the search in that direction, so each match
    // scans a bounded distance: without the limit, every match in a long line with no whitespace (a CSV row of
    // identifiers) scanned to both ends of the line.
    private const int MaxContextWordLength = 64;

    // Up to count words ending at index start, nearest last. Characters between the previous whitespace and
    // the match count as a word, so the "SIN:" of "SIN:046454286" is seen.
    private static List<string> WordsBefore(string input, int start, int count)
    {
        var words = new List<string>();
        var i = start;
        while (words.Count < count)
        {
            while (i > 0 && char.IsWhiteSpace(input[i - 1])) i--;
            if (i == 0) break;
            var wordEnd = i;
            while (i > 0 && !char.IsWhiteSpace(input[i - 1]) && wordEnd - i < MaxContextWordLength) i--;
            if (i > 0 && !char.IsWhiteSpace(input[i - 1])) break;
            AddContextWord(words, input[i..wordEnd]);
        }

        words.Reverse();
        return words;
    }

    // Up to count words starting at index end, nearest first.
    private static List<string> WordsAfter(string input, int end, int count)
    {
        var words = new List<string>();
        var i = end;
        while (words.Count < count)
        {
            while (i < input.Length && char.IsWhiteSpace(input[i])) i++;
            if (i == input.Length) break;
            var wordStart = i;
            while (i < input.Length && !char.IsWhiteSpace(input[i]) && i - wordStart < MaxContextWordLength) i++;
            if (i < input.Length && !char.IsWhiteSpace(input[i])) break;
            AddContextWord(words, input[wordStart..i]);
        }

        return words;
    }

    // A word that is only punctuation, such as a dash between two clauses, still counts toward the window
    // so the window is the same number of words wherever punctuation falls; it can never match a term.
    private static void AddContextWord(List<string> words, string word) => words.Add(NormalizeContextWord(word));

    private static string NormalizeContextWord(string word)
    {
        var first = 0;
        var last = word.Length - 1;
        while (first <= last && !char.IsLetterOrDigit(word[first])) first++;
        while (last >= first && !char.IsLetterOrDigit(word[last])) last--;
        return word[first..(last + 1)].ToLowerInvariant();
    }

    private static bool ContainsPhrase(List<string> words, string[] phrase)
    {
        for (var i = 0; i + phrase.Length <= words.Count; i++)
        {
            var found = true;
            for (var j = 0; j < phrase.Length && found; j++)
                found = words[i + j] == phrase[j];
            if (found) return true;
        }

        return false;
    }

    /// <summary>
    ///     Applies post-processing rules to refine or remove spans after initial matching.
    ///     The default implementation returns the spans unchanged.
    /// </summary>
    /// <param name="spans">The list of spans produced by the initial matching pass.</param>
    /// <param name="input">The original input text.</param>
    /// <returns>The refined list of spans.</returns>
    protected IList<Span> PostFilter(IList<Span> spans, string input)
    {
        if (PostFiltersConfig.RemoveTrailingNewLines)
            spans = TrailingNewLinesPostFilter.Apply(spans);
        if (PostFiltersConfig.RemoveTrailingPeriods)
            spans = TrailingPeriodPostFilter.Apply(spans);
        if (PostFiltersConfig.RemoveTrailingSpaces)
            spans = TrailingSpacePostFilter.Apply(spans);
        spans = IgnoredTermsPostFilter.Apply(spans, Ignored);
        spans = IgnoredPatternsPostFilter.Apply(spans, IgnoredPatterns, RegexTimeout, RecordRegexTimeout);
        return spans;
    }

    /// <summary>
    ///     Returns all whitespace-delimited n-grams of every length from <paramref name="length" /> down to 1,
    ///     each paired with its character <see cref="Position" /> in <paramref name="text" />.
    /// </summary>
    protected static List<(Position Position, string Ngram)> GetNgramsUpToLength(string text, int length)
    {
        var ngrams = new List<(Position, string)>();
        for (var n = length; n > 0; n--)
        {
            ngrams.AddRange(GetNgramsOfLength(text, n));
        }

        return ngrams;
    }

    /// <summary>
    ///     Returns the whitespace-delimited n-grams of exactly <paramref name="length" /> words, each paired
    ///     with its character <see cref="Position" /> in <paramref name="text" />. Any whitespace character
    ///     (space, tab, line break, non-breaking space) separates words, and a run of it counts once.
    ///     <para>
    ///         Each position is taken from the words the n-gram was built from, tracked while splitting.
    ///         Searching the text for the n-gram afterwards instead reported the first place that text
    ///         occurred, which is a different occurrence whenever the same words appear earlier: a term
    ///         inside a longer preceding word took that word's position, so the wrong characters were
    ///         redacted and the real value was left behind, and a repeated term collapsed onto one
    ///         position. See philterd/phileas-dotnet#119.
    ///     </para>
    /// </summary>
    protected static List<(Position Position, string Ngram)> GetNgramsOfLength(string text, int length)
    {
        var ngrams = new List<(Position, string)>();
        if (length <= 0) return ngrams;

        // Words are runs of non-whitespace. Splitting on ' ' alone left a line break or tab inside a
        // word, so "Dear\nJohn" was one token and neither name matched. See philterd/phileas-dotnet#150.
        var words = new List<Position>();
        var i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i == text.Length) break;
            var start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
            words.Add(new Position(start, i));
        }

        for (var w = 0; w + length <= words.Count; w++)
        {
            var start = words[w].Start;
            var end = words[w + length - 1].End;
            ngrams.Add((new Position(start, end), text.Substring(start, end - start)));
        }

        return ngrams;
    }

    /// <summary>
    ///     Collapses each run of whitespace in <paramref name="text" /> to a single space, so a multi-word
    ///     n-gram broken across a line or tab compares equal to the same term written with spaces.
    /// </summary>
    protected static string NormalizeWhitespace(string text)
    {
        var needed = false;
        for (var i = 0; i < text.Length && !needed; i++)
            needed = char.IsWhiteSpace(text[i]) && (text[i] != ' ' || (i > 0 && char.IsWhiteSpace(text[i - 1])));
        if (!needed) return text;

        var builder = new System.Text.StringBuilder(text.Length);
        var inWhitespace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!inWhitespace) builder.Append(' ');
                inWhitespace = true;
            }
            else
            {
                builder.Append(c);
                inWhitespace = false;
            }
        }

        return builder.ToString();
    }
}
