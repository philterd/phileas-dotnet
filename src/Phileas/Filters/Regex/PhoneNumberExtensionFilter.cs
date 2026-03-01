using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Rules.Regex;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Regex;

public class PhoneNumberExtensionFilter : RegexFilter
{
    private static readonly Analyzer PhoneExtAnalyzer = new Analyzer(
        new FilterPattern.Builder().WithPattern(@"\b(?:ext|x|extension)\.?\s*[0-9]{1,6}\b", RegexOptions.IgnoreCase).WithInitialConfidence(0.80).Build()
    );

    public PhoneNumberExtensionFilter(FilterConfiguration configuration) : base(FilterType.PhoneNumberExtension, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, PhoneExtAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
