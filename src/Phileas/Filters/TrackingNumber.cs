using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class TrackingNumber : AbstractPolicyFilter
{
    [JsonPropertyName("trackingNumberFilterStrategies")]
    public List<TrackingNumberFilterStrategy>? Strategies { get; set; }
}
