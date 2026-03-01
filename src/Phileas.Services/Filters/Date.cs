using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class Date : AbstractPolicyFilter
{
    [JsonPropertyName("dateFilterStrategies")]
    public List<DateFilterStrategy>? Strategies { get; set; }
}
