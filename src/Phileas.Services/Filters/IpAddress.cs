using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class IpAddress : AbstractPolicyFilter
{
    [JsonPropertyName("ipAddressFilterStrategies")]
    public List<IpAddressFilterStrategy>? Strategies { get; set; }
}
