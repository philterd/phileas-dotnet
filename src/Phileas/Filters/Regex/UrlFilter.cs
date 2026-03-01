using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Filters.Rules.Regex;
using Phileas.Model.Filtering;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Regex;

public class UrlFilter : RegexFilter
{
    private static readonly Analyzer UrlAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b(?:https?|ftp)://[^\s/$.?#].[^\s]*\b", RegexOptions.IgnoreCase).WithInitialConfidence(0.95).Build(),
        new FilterPattern.Builder().WithPattern(@"\bwww\.[^\s/$.?#].[^\s]*\b", RegexOptions.IgnoreCase).WithInitialConfidence(0.90).Build()
    );

    public UrlFilter(FilterConfiguration configuration) : base(FilterType.Url, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, UrlAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
