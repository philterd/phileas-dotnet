using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Filters.Rules.Regex;
using Phileas.Model.Filtering;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Services.Filters.Regex;

public class IpAddressFilter : RegexFilter
{
    private static readonly Analyzer IpAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b").WithInitialConfidence(0.95).Build(),
        new FilterPattern.Builder().WithPattern(@"\b(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}\b").WithInitialConfidence(0.95).Build()
    );

    public IpAddressFilter(FilterConfiguration configuration) : base(FilterType.IpAddress, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, IpAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
