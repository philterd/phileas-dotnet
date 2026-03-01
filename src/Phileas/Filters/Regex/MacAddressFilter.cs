using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Filters.Rules.Regex;
using Phileas.Model.Filtering;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Regex;

public class MacAddressFilter : RegexFilter
{
    private static readonly Analyzer MacAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b([0-9A-Fa-f]{2}[:\-]){5}([0-9A-Fa-f]{2})\b").WithInitialConfidence(0.95).Build()
    );

    public MacAddressFilter(FilterConfiguration configuration) : base(FilterType.MacAddress, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, MacAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
