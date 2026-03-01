using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Rules.Regex;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Regex;

public class DriversLicenseFilter : RegexFilter
{
    private static readonly Analyzer DriversLicenseAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b[A-Z][0-9]{7}\b").WithInitialConfidence(0.70).Build(),
        new FilterPattern.Builder().WithPattern(@"\b[A-Z]{2}[0-9]{6}\b").WithInitialConfidence(0.70).Build(),
        new FilterPattern.Builder().WithPattern(@"\b[0-9]{9}\b").WithInitialConfidence(0.50).Build()
    );

    public DriversLicenseFilter(FilterConfiguration configuration) : base(FilterType.DriversLicenseNumber, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, DriversLicenseAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
