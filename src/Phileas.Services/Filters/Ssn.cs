using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class Ssn : AbstractPolicyFilter
{
    [JsonPropertyName("ssnFilterStrategies")]
    public List<SsnFilterStrategy>? Strategies { get; set; }
}
