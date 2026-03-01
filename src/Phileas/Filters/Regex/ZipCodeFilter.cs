using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Filters.Rules.Regex;
using Phileas.Model.Filtering;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Regex;

public class ZipCodeFilter : RegexFilter
{
    private static readonly Analyzer ZipAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b\d{5}(?:-\d{4})?\b").WithInitialConfidence(0.60).Build()
    );

    public ZipCodeFilter(FilterConfiguration configuration) : base(FilterType.ZipCode, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, ZipAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
