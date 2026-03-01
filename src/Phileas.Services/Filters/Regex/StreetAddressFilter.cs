using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Filters.Rules.Regex;
using Phileas.Model.Filtering;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Services.Filters.Regex;

public class StreetAddressFilter : RegexFilter
{
    private static readonly Analyzer StreetAddressAnalyzer = new Analyzer(
        new FilterPattern.Builder()
            .WithPattern(@"\b\d{1,5}\s+([A-Za-z]+\s?){1,5}(Street|St|Avenue|Ave|Boulevard|Blvd|Drive|Dr|Road|Rd|Lane|Ln|Way|Court|Ct|Place|Pl|Circle|Cir|Highway|Hwy|Parkway|Pkwy|Square|Sq|Trail|Trl|Terrace|Ter)\b\.?", RegexOptions.IgnoreCase)
            .WithInitialConfidence(0.85)
            .Build()
    );

    public StreetAddressFilter(FilterConfiguration configuration) : base(FilterType.StreetAddress, configuration) { }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, StreetAddressAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
