using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class PhoneNumber : AbstractPolicyFilter
{
    [JsonPropertyName("phoneNumberFilterStrategies")]
    public List<PhoneNumberFilterStrategy>? Strategies { get; set; }
}
