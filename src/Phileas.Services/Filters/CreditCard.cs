using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class CreditCard : AbstractPolicyFilter
{
    [JsonPropertyName("creditCardFilterStrategies")]
    public List<CreditCardFilterStrategy>? Strategies { get; set; }
}
