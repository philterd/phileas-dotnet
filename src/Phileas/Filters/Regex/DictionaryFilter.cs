using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Rules.Regex;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Regex;

public class DictionaryFilter : RegexFilter
{
    private readonly Analyzer _analyzer;

    public DictionaryFilter(FilterConfiguration configuration, IEnumerable<string> terms)
        : base(FilterType.Dictionary, configuration)
    {
        var patterns = terms
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => new FilterPattern.Builder()
                .WithPattern(
                    @"(?<!\w)" + System.Text.RegularExpressions.Regex.Escape(t) + @"(?!\w)",
                    RegexOptions.IgnoreCase)
                .WithInitialConfidence(1.0)
                .Build())
            .ToArray();

        _analyzer = new Analyzer(patterns);
    }

    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, _analyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}
