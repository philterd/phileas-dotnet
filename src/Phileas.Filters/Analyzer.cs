using Phileas.Model.Filtering;

namespace Phileas.Filters;

public class Analyzer
{
    public ISet<string>? ContextualTerms { get; }
    public IList<FilterPattern> FilterPatterns { get; }

    public Analyzer(params FilterPattern[] patterns)
    {
        FilterPatterns = patterns.ToList();
    }

    public Analyzer(ISet<string> contextualTerms, params FilterPattern[] patterns)
    {
        ContextualTerms = contextualTerms;
        FilterPatterns = patterns.ToList();
    }
}
