using Phileas.Model.Filtering;
using Phileas.Policy;

namespace Phileas.Filters.Rules;

public abstract class RulesFilter : AbstractFilter
{
    protected RulesFilter(FilterType filterType, FilterConfiguration configuration)
        : base(filterType, configuration) { }

    public virtual IList<Span> PostFilter(IList<Span> spans, string input) => spans;
}
