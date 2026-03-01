using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class BankRoutingNumber : AbstractPolicyFilter
{
    [JsonPropertyName("bankRoutingNumberFilterStrategies")]
    public List<BankRoutingNumberFilterStrategy>? Strategies { get; set; }
}
