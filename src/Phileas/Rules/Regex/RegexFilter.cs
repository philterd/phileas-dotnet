using Phileas.Filters;
using Phileas.Model.Filtering;

namespace Phileas.Rules.Regex;

public abstract class RegexFilter : RulesFilter
{
    protected Analyzer? Analyzer { get; set; }

    protected RegexFilter(FilterType filterType, FilterConfiguration configuration)
        : base(filterType, configuration) { }

    protected IList<Span> FindSpans(Phileas.Policy.Policy policy, Analyzer analyzer, string input, string context, int piece)
    {
        var spans = new List<Span>();

        if (!policy.Identifiers.HasFilter(FilterType)) return spans;

        foreach (var filterPattern in analyzer.FilterPatterns)
        {
            var matches = filterPattern.Pattern.Matches(input);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                string matchText;
                int matchStart;
                int matchEnd;

                if (filterPattern.GroupNumber > 0 && match.Groups.Count > filterPattern.GroupNumber)
                {
                    var group = match.Groups[filterPattern.GroupNumber];
                    matchText = group.Value;
                    matchStart = group.Index;
                    matchEnd = group.Index + group.Length;
                }
                else
                {
                    matchText = match.Value;
                    matchStart = match.Index;
                    matchEnd = match.Index + match.Length;
                }

                if (string.IsNullOrWhiteSpace(matchText)) continue;
                if (IsIgnored(matchText)) continue;
                if (Span.DoesSpanExist(matchStart, matchEnd, spans)) continue;

                var window = GetWindow(input, matchStart, matchEnd);
                var confidence = filterPattern.InitialConfidence;

                var replacement = GetReplacement(policy, context, matchText, window, confidence, filterPattern.Classification ?? Classification, filterPattern);

                var span = Span.Make(
                    matchStart, matchEnd,
                    FilterType, context, confidence, matchText,
                    replacement.Value, replacement.Salt,
                    false, replacement.Applied,
                    window, Priority);
                span.AlwaysValid = filterPattern.AlwaysValid;
                span.Classification = filterPattern.Classification ?? Classification;

                spans.Add(span);
            }
        }

        return spans;
    }
}
