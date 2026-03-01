using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class BitcoinAddress : AbstractPolicyFilter
{
    [JsonPropertyName("bitcoinAddressFilterStrategies")]
    public List<BitcoinAddressFilterStrategy>? Strategies { get; set; }
}
