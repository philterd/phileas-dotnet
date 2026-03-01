using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Filters.Rules.Regex;
using Phileas.Model.Filtering;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Regex;

public class DateFilter : RegexFilter
{
    private static readonly Analyzer DateAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b(0?[1-9]|1[012])[\/\-\.](0?[1-9]|[12][0-9]|3[01])[\/\-\.](19|20)?\d{2}\b").WithInitialConfidence(0.85).Build(),
        new FilterPattern.Builder().WithPattern(@"\b(January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},?\s+\d{4}\b", RegexOptions.IgnoreCase).WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder().WithPattern(@"\b\d{1,2}\s+(January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{4}\b", RegexOptions.IgnoreCase).WithInitialConfidence(0.90).Build(),
        new FilterPattern.Builder().WithPattern(@"\b(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[.\s]\s*\d{1,2},?\s*\d{4}\b", RegexOptions.IgnoreCase).WithInitialConfidence(0.85).Build()
    );

    public DateFilter(FilterConfiguration configuration) : base(FilterType.Date, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, DateAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
