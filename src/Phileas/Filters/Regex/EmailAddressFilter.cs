using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Rules.Regex;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Regex;

public class EmailAddressFilter : RegexFilter
{
    private static readonly Analyzer EmailAnalyzer = new Analyzer(
        new FilterPattern.Builder()
            .WithPattern(@"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b")
            .WithInitialConfidence(0.99)
            .Build()
    );

    public EmailAddressFilter(FilterConfiguration configuration) : base(FilterType.EmailAddress, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, EmailAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
