using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Filters.Rules.Regex;
using Phileas.Model.Filtering;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Regex;

public class BitcoinAddressFilter : RegexFilter
{
    private static readonly Analyzer BitcoinAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b[13][a-km-zA-HJ-NP-Z1-9]{25,34}\b").WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder().WithPattern(@"\bbc1[a-z0-9]{6,87}\b").WithInitialConfidence(0.90).Build()
    );

    public BitcoinAddressFilter(FilterConfiguration configuration) : base(FilterType.BitcoinAddress, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, BitcoinAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
