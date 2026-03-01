using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class PhEye : AbstractPolicyFilter
{
    [JsonPropertyName("phEyeFilterStrategies")]
    public List<PhEyeFilterStrategy>? Strategies { get; set; }

    [JsonPropertyName("phEyeConfiguration")]
    public PhEyeConfiguration PhEyeConfiguration { get; set; } = new PhEyeConfiguration();

    [JsonPropertyName("removePunctuation")]
    public bool RemovePunctuation { get; set; } = false;

    [JsonPropertyName("thresholds")]
    public Dictionary<string, double> Thresholds { get; set; } = new Dictionary<string, double>();
}
