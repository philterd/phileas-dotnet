using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class IbanCode : AbstractPolicyFilter
{
    [JsonPropertyName("ibanCodeFilterStrategies")]
    public List<IbanCodeFilterStrategy>? Strategies { get; set; }
}
