using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class DriversLicense : AbstractPolicyFilter
{
    [JsonPropertyName("driversLicenseFilterStrategies")]
    public List<DriversLicenseFilterStrategy>? Strategies { get; set; }
}
