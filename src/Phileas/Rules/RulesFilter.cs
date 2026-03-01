using Phileas.Filters;
using Phileas.Model.Filtering;

namespace Phileas.Rules;

public abstract class RulesFilter : AbstractFilter
{
    protected RulesFilter(FilterType filterType, FilterConfiguration configuration)
        : base(filterType, configuration) { }

    public virtual IList<Span> PostFilter(IList<Span> spans, string input) => spans;
}
