using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class Age : AbstractPolicyFilter
{
    [JsonPropertyName("ageFilterStrategies")]
    public List<AgeFilterStrategy>? Strategies { get; set; }
}
