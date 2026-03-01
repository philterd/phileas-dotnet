using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class StateAbbreviation : AbstractPolicyFilter
{
    [JsonPropertyName("stateAbbreviationFilterStrategies")]
    public List<StateAbbreviationFilterStrategy>? Strategies { get; set; }
}
