using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class PassportNumber : AbstractPolicyFilter
{
    [JsonPropertyName("passportNumberFilterStrategies")]
    public List<PassportNumberFilterStrategy>? Strategies { get; set; }
}
