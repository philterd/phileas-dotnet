using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Filters.Rules.Regex;
using Phileas.Model.Filtering;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Services.Filters.Regex;

public class PassportNumberFilter : RegexFilter
{
    private static readonly Analyzer PassportAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b[A-Z]{1,2}[0-9]{6,9}\b").WithInitialConfidence(0.75).Build()
    );

    public PassportNumberFilter(FilterConfiguration configuration) : base(FilterType.PassportNumber, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, PassportAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
