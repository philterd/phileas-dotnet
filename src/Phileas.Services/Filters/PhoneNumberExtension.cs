using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class PhoneNumberExtension : AbstractPolicyFilter
{
    [JsonPropertyName("phoneNumberExtensionFilterStrategies")]
    public List<PhoneNumberExtensionFilterStrategy>? Strategies { get; set; }
}
