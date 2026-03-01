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

using Phileas.Model.Filtering;
using Phileas.Policy;

namespace Phileas.Filters;

public abstract class AbstractFilter
{
    protected readonly FilterType FilterType;
    protected readonly IList<AbstractFilterStrategy> Strategies;
    protected ISet<string> Ignored;
    protected IList<IgnoredPattern> IgnoredPatterns;
    protected readonly Crypto? Crypto;
    protected readonly Fpe? Fpe;
    protected int WindowSize;
    protected int Priority;
    protected string? Classification;
    protected readonly Phileas.Policy.PostFilters PostFiltersConfig;

    protected AbstractFilter(FilterType filterType, FilterConfiguration configuration)
    {
        FilterType = filterType;
        Strategies = configuration.Strategies ?? new List<AbstractFilterStrategy>();
        Ignored = configuration.Ignored ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IgnoredPatterns = configuration.IgnoredPatterns ?? new List<IgnoredPattern>();
        Crypto = configuration.Crypto;
        Fpe = configuration.Fpe;
        WindowSize = configuration.WindowSize;
        Priority = configuration.Priority;
        PostFiltersConfig = configuration.PostFilters ?? new Phileas.Policy.PostFilters();
    }

    public abstract Filtered Filter(Phileas.Policy.Policy policy, string context, int piece, string input);

    public FilterType GetFilterType() => FilterType;

    protected bool IsIgnored(string token)
    {
        if (Ignored.Contains(token)) return true;
        foreach (var pattern in IgnoredPatterns)
        {
            if (pattern.Pattern == null) continue;
            var options = pattern.CaseSensitive
                ? System.Text.RegularExpressions.RegexOptions.None
                : System.Text.RegularExpressions.RegexOptions.IgnoreCase;
            if (System.Text.RegularExpressions.Regex.IsMatch(token, pattern.Pattern, options))
                return true;
        }
        return false;
    }

    protected string[] GetWindow(string text, int characterStart, int characterEnd)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return Array.Empty<string>();

        int spanStartWord = -1;
        int spanEndWord = -1;
        int charPos = 0;

        for (int i = 0; i < words.Length; i++)
        {
            int wordStart = text.IndexOf(words[i], charPos, StringComparison.Ordinal);
            if (wordStart < 0) { charPos += words[i].Length; continue; }
            int wordEnd = wordStart + words[i].Length;
            if (spanStartWord < 0 && wordEnd > characterStart) spanStartWord = i;
            if (wordStart < characterEnd) spanEndWord = i;
            charPos = wordEnd;
        }

        if (spanStartWord < 0) spanStartWord = 0;
        if (spanEndWord < 0) spanEndWord = words.Length - 1;

        int windowStart = Math.Max(0, spanStartWord - WindowSize);
        int windowEnd = Math.Min(words.Length - 1, spanEndWord + WindowSize);

        return words.Skip(windowStart).Take(windowEnd - windowStart + 1).ToArray();
    }

    protected Replacement GetReplacement(Phileas.Policy.Policy policy, string context, string token, string[] window, double confidence, string? classification, FilterPattern? filterPattern)
    {
        foreach (var strategy in Strategies)
        {
            if (strategy.EvaluateCondition(context, token, window, confidence, classification, filterPattern))
                return strategy.GetReplacement(context, token, window, confidence, classification, filterPattern, Crypto, Fpe);
        }

        return new Replacement("{{{REDACTED-" + FilterType.GetFilterTypeName() + "}}}", string.Empty, true);
    }
}
