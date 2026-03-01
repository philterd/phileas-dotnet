using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Rules.Regex;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Regex;

public class DictionaryFilter : RegexFilter
{
    private readonly Analyzer _analyzer;
    private readonly bool _fuzzy;
    private readonly string _level;
    private readonly string[] _terms;

    public DictionaryFilter(FilterConfiguration configuration, IEnumerable<string> terms, bool fuzzy = false, string level = "low")
        : base(FilterType.Dictionary, configuration)
    {
        _fuzzy = fuzzy;
        _level = level;
        _terms = terms.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();

        var patterns = _terms
            .Select(t => new FilterPattern.Builder()
                .WithPattern(
                    @"(?<!\w)" + System.Text.RegularExpressions.Regex.Escape(t) + @"(?!\w)",
                    RegexOptions.IgnoreCase)
                .WithInitialConfidence(1.0)
                .Build())
            .ToArray();

        _analyzer = new Analyzer(patterns);
    }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, _analyzer, input, context, piece);

        if (_fuzzy)
            spans = FindFuzzySpans(policy, input, context, piece, spans);

        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }

    private IList<Span> FindFuzzySpans(PhileasPolicy policy, string input, string context, int piece, IList<Span> exactSpans)
    {
        var (maxDistance, confidence) = GetFuzzyParameters(_level);

        var allSpans = new List<Span>(exactSpans);

        // Tokenize the input on non-word characters to find candidate tokens
        var tokenMatches = System.Text.RegularExpressions.Regex.Matches(input, @"\b\w+\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match tokenMatch in tokenMatches)
        {
            var token = tokenMatch.Value;
            var tokenStart = tokenMatch.Index;
            var tokenEnd = tokenMatch.Index + tokenMatch.Length;

            if (IsIgnored(token)) continue;
            if (Span.DoesSpanExist(tokenStart, tokenEnd, allSpans)) continue;

            foreach (var term in _terms)
            {
                // Skip if same length difference exceeds max distance (fast check)
                if (Math.Abs(token.Length - term.Length) > maxDistance) continue;

                var distance = LevenshteinDistance(token, term);
                if (distance > 0 && distance <= maxDistance)
                {
                    var window = GetWindow(input, tokenStart, tokenEnd);
                    var replacement = GetReplacement(policy, context, token, window, confidence, null, null);

                    var span = Span.Make(
                        tokenStart, tokenEnd,
                        FilterType, context, confidence, token,
                        replacement.Value, replacement.Salt,
                        false, replacement.Applied,
                        window, Priority);

                    allSpans.Add(span);
                    break;
                }
            }
        }

        return allSpans;
    }

    private static (int maxDistance, double confidence) GetFuzzyParameters(string level) =>
        level?.ToLowerInvariant() switch
        {
            "medium" => (2, 0.75),
            "high"   => (3, 0.6),
            _        => (1, 0.9)   // "low" or unrecognized
        };

    internal static int LevenshteinDistance(string s, string t)
    {
        // Case-insensitive comparison
        s = s.ToLowerInvariant();
        t = t.ToLowerInvariant();

        int m = s.Length, n = t.Length;
        var d = new int[m + 1, n + 1];

        for (int i = 0; i <= m; i++) d[i, 0] = i;
        for (int j = 0; j <= n; j++) d[0, j] = j;

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[m, n];
    }
}
