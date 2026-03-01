using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Rules.Regex;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Regex;

public class IbanCodeFilter : RegexFilter
{
    private static readonly Analyzer IbanAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b[A-Z]{2}[0-9]{2}[A-Z0-9]{4}[0-9]{7}([A-Z0-9]?){0,16}\b").WithInitialConfidence(0.90).Build()
    );

    public IbanCodeFilter(FilterConfiguration configuration) : base(FilterType.IbanCode, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, IbanAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
