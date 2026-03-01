using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Rules.Regex;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Regex;

public class VinFilter : RegexFilter
{
    private static readonly Analyzer VinAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b[A-HJ-NPR-Z0-9]{17}\b").WithInitialConfidence(0.80).Build()
    );

    public VinFilter(FilterConfiguration configuration) : base(FilterType.Vin, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, VinAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
