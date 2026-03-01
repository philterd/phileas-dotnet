using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Rules.Regex;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Regex;

public class TrackingNumberFilter : RegexFilter
{
    private static readonly Analyzer TrackingAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b1Z[0-9A-Z]{16}\b").WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder().WithPattern(@"\b[0-9]{20,22}\b").WithInitialConfidence(0.70).Build(),
        new FilterPattern.Builder().WithPattern(@"\b[0-9]{12,15}\b").WithInitialConfidence(0.60).Build()
    );

    public TrackingNumberFilter(FilterConfiguration configuration) : base(FilterType.TrackingNumber, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, TrackingAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
