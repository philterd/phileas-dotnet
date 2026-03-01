using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class Currency : AbstractPolicyFilter
{
    [JsonPropertyName("currencyFilterStrategies")]
    public List<CurrencyFilterStrategy>? Strategies { get; set; }
}
