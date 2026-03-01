using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class Vin : AbstractPolicyFilter
{
    [JsonPropertyName("vinFilterStrategies")]
    public List<VinFilterStrategy>? Strategies { get; set; }
}
