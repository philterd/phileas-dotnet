using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Rules.Regex;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Regex;

public class PhoneNumberFilter : RegexFilter
{
    private static readonly Analyzer PhoneAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b(\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b").WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder().WithPattern(@"\b\d{3}[-.\s]\d{3}[-.\s]\d{4}\b").WithInitialConfidence(0.90).Build()
    );

    public PhoneNumberFilter(FilterConfiguration configuration) : base(FilterType.PhoneNumber, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, PhoneAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
