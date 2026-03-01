using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class StreetAddress : AbstractPolicyFilter
{
    [JsonPropertyName("streetAddressFilterStrategies")]
    public List<StreetAddressFilterStrategy>? Strategies { get; set; }
}
