using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class Url : AbstractPolicyFilter
{
    [JsonPropertyName("urlFilterStrategies")]
    public List<UrlFilterStrategy>? Strategies { get; set; }
}
