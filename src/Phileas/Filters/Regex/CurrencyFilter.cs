using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Rules.Regex;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Regex;

public class CurrencyFilter : RegexFilter
{
    private static readonly Analyzer CurrencyAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\$\s?[0-9,]+(\.[0-9]{1,2})?(?:\s?(million|billion|trillion|thousand))?", RegexOptions.IgnoreCase).WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder().WithPattern(@"\b[0-9,]+(\.[0-9]{1,2})?\s?(USD|EUR|GBP|JPY|CAD|AUD|CHF|CNY)\b").WithInitialConfidence(0.90).Build()
    );

    public CurrencyFilter(FilterConfiguration configuration) : base(FilterType.Currency, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, CurrencyAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
