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
using Phileas.Policy;
using Phileas.Services;
using Phileas.Services.Anonymization;

namespace Phileas.Filters;

/// <summary>
///     Base class for all Phileas filters. Stores common configuration (strategies, ignored terms,
///     crypto settings, window size, and priority) and provides helper methods used by concrete filters.
/// </summary>
public abstract class AbstractFilter
{
    /// <summary>AES encryption settings used by the <c>CRYPTO_REPLACE</c> strategy, or <see langword="null" /> if not configured.</summary>
    protected readonly Crypto? Crypto;

    /// <summary>The entity type detected by this filter instance.</summary>
    protected readonly FilterType FilterType;

    /// <summary>Format-preserving encryption settings used by the <c>FPE_ENCRYPT_REPLACE</c> strategy, or <see langword="null" /> if not configured.</summary>
    protected readonly Fpe? Fpe;

    /// <summary>Post-filter configuration that controls trailing-character trimming.</summary>
    protected readonly Policy.PostFilters PostFiltersConfig;

    /// <summary>The ordered list of replacement strategies to apply when a match is found.</summary>
    protected readonly IList<AbstractFilterStrategy> Strategies;

    /// <summary>Optional classification label applied to all matches produced by this filter.</summary>
    protected string? Classification;

    /// <summary>The set of exact token values that should not be filtered.</summary>
    protected ISet<string> Ignored;

    /// <summary>The list of regex-based patterns for tokens that should not be filtered.</summary>
    protected IList<IgnoredPattern> IgnoredPatterns;

    /// <summary>Priority used when resolving overlapping spans from different filters.</summary>
    protected int Priority;

    /// <summary>Number of words on each side of a match to include in the context window.</summary>
    protected int WindowSize;

    /// <summary>
    ///     Initializes the filter with the given <paramref name="filterType" /> and <paramref name="configuration" />.
    /// </summary>
    /// <param name="filterType">The type of entity this filter detects.</param>
    /// <param name="configuration">Runtime configuration including strategies, ignored terms, and window size.</param>
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
        PostFiltersConfig = configuration.PostFilters ?? new Policy.PostFilters();

        // Initialize the strategy-specific anonymization services (mirrors the Java Filter constructor):
        // each strategy gets the service for this filter type, drawing FROM_LIST when the strategy
        // carries anonymization candidates and generating realistic values otherwise.
        var random = new Random();
        foreach (var strategy in Strategies)
        {
            if (strategy.AnonymizationService != null) continue;
            strategy.AnonymizationService = AnonymizationServiceFactory.Create(
                filterType,
                strategy.ContextService ?? new InMemoryContextService(),
                random,
                strategy.AnonymizationCandidates,
                AnonymizationMethods.FromString(strategy.AnonymizationMethod ?? "realistic"));
        }
    }

    /// <summary>
    ///     Scans <paramref name="input" /> for entities, applies the configured replacement strategy
    ///     to each match, and returns a <see cref="Filtered" /> containing all detected spans.
    /// </summary>
    /// <param name="policy">The active policy providing per-filter settings.</param>
    /// <param name="context">The context identifier used for referential-integrity replacements.</param>
    /// <param name="piece">Zero-based piece index within a multi-part document.</param>
    /// <param name="input">The plain-text string to search.</param>
    /// <returns>A <see cref="Filtered" /> containing all detected and (optionally) replaced spans.</returns>
    public abstract Filtered Filter(Policy.Policy policy, string context, int piece, string input);

    /// <summary>Returns the replacement strategies configured on this filter.</summary>
    public IList<AbstractFilterStrategy> GetStrategies()
    {
        return Strategies;
    }

    /// <summary>Returns the <see cref="FilterType" /> handled by this filter instance.</summary>
    public FilterType GetFilterType()
    {
        return FilterType;
    }

    /// <summary>
    ///     Determines whether <paramref name="token" /> should be excluded from filtering based on
    ///     the configured ignored terms and ignored-pattern rules.
    /// </summary>
    /// <param name="token">The candidate entity text to check.</param>
    /// <returns><see langword="true" /> if the token is on the ignore list; otherwise <see langword="false" />.</returns>
    protected bool IsIgnored(string token)
    {
        if (Ignored.Contains(token)) return true;
        foreach (var pattern in IgnoredPatterns)
        {
            if (pattern.Pattern == null) continue;
            var options = pattern.CaseSensitive
                ? RegexOptions.None
                : RegexOptions.IgnoreCase;
            if (Regex.IsMatch(token, pattern.Pattern, options))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Returns the words that appear within <see cref="WindowSize" /> words of the matched entity,
    ///     providing surrounding context for condition evaluation.
    /// </summary>
    /// <param name="text">The full input text.</param>
    /// <param name="characterStart">Start offset of the entity.</param>
    /// <param name="characterEnd">Exclusive end offset of the entity.</param>
    /// <returns>An array of words surrounding the entity, up to <see cref="WindowSize" /> words on each side.</returns>
    protected string[] GetWindow(string text, int characterStart, int characterEnd)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return Array.Empty<string>();

        var spanStartWord = -1;
        var spanEndWord = -1;
        var charPos = 0;

        for (var i = 0; i < words.Length; i++)
        {
            var wordStart = text.IndexOf(words[i], charPos, StringComparison.Ordinal);
            if (wordStart < 0)
            {
                charPos += words[i].Length;
                continue;
            }

            var wordEnd = wordStart + words[i].Length;
            if (spanStartWord < 0 && wordEnd > characterStart) spanStartWord = i;
            if (wordStart < characterEnd) spanEndWord = i;
            charPos = wordEnd;
        }

        if (spanStartWord < 0) spanStartWord = 0;
        if (spanEndWord < 0) spanEndWord = words.Length - 1;

        var windowStart = Math.Max(0, spanStartWord - WindowSize);
        var windowEnd = Math.Min(words.Length - 1, spanEndWord + WindowSize);

        return words.Skip(windowStart).Take(windowEnd - windowStart + 1).ToArray();
    }

    /// <summary>
    ///     Iterates the configured strategies and returns the replacement produced by the first strategy
    ///     whose condition is satisfied. Falls back to a default redaction token when no strategy matches.
    /// </summary>
    /// <param name="policy">The active policy.</param>
    /// <param name="context">The context identifier.</param>
    /// <param name="token">The detected entity text.</param>
    /// <param name="window">Surrounding context words.</param>
    /// <param name="confidence">Confidence score of the detection.</param>
    /// <param name="classification">Optional entity classification label.</param>
    /// <param name="filterPattern">The <see cref="FilterPattern" /> that produced the match, or <see langword="null" />.</param>
    /// <returns>A <see cref="Replacement" /> containing the replacement value and salt.</returns>
    protected Replacement GetReplacement(Policy.Policy policy, string context, string token, string[] window,
        double confidence, string? classification, FilterPattern? filterPattern)
    {
        foreach (var strategy in Strategies)
            if (strategy.EvaluateCondition(context, token, window, confidence, classification, filterPattern))
                return strategy.GetReplacement(context, token, window, confidence, classification, filterPattern,
                    Crypto, Fpe);

        return new Replacement("{{{REDACTED-" + FilterType.GetFilterTypeName() + "}}}", string.Empty);
    }
}