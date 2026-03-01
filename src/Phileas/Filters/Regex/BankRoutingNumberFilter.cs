using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Rules.Regex;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Regex;

public class BankRoutingNumberFilter : RegexFilter
{
    private static readonly Analyzer BankRoutingAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b[0-9]{9}\b").WithInitialConfidence(0.50).Build()
    );

    public BankRoutingNumberFilter(FilterConfiguration configuration) : base(FilterType.BankRoutingNumber, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, BankRoutingAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
