using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class ZipCode : AbstractPolicyFilter
{
    [JsonPropertyName("zipCodeFilterStrategies")]
    public List<ZipCodeFilterStrategy>? Strategies { get; set; }
}
