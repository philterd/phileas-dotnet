using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Filters.Rules.Regex;
using Phileas.Model.Filtering;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Services.Filters.Regex;

public class AgeFilter : RegexFilter
{
    private static readonly Analyzer AgeAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b[0-9.]+[\s]*(year|years|yrs|yr|yo)(\.?)(\s)*(old)?\b", RegexOptions.IgnoreCase).WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder().WithPattern(@"\b(age)(d)?(\s)*[0-9.]+\b", RegexOptions.IgnoreCase).WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder().WithPattern(@"\b[0-9.]+[-]*(year|years|yrs|yr|yo)(\.?)(-)*(old)?\b", RegexOptions.IgnoreCase).WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder().WithPattern(@"\b([0-9]{1,3}) (y\/o)\b", RegexOptions.IgnoreCase).WithInitialConfidence(0.90).Build()
    );

    public AgeFilter(FilterConfiguration configuration) : base(FilterType.Age, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, AgeAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
