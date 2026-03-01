using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class EmailAddress : AbstractPolicyFilter
{
    [JsonPropertyName("emailAddressFilterStrategies")]
    public List<EmailAddressFilterStrategy>? Strategies { get; set; }
}
