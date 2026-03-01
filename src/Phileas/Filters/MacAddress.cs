using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class MacAddress : AbstractPolicyFilter
{
    [JsonPropertyName("macAddressFilterStrategies")]
    public List<MacAddressFilterStrategy>? Strategies { get; set; }
}
