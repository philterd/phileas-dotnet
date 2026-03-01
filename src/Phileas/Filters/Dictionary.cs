using System.Text.Json.Serialization;
using Phileas.Filters;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class Dictionary : AbstractPolicyFilter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("terms")]
    public List<string> Terms { get; set; } = new List<string>();

    [JsonPropertyName("fuzzy")]
    public bool Fuzzy { get; set; } = false;

    [JsonPropertyName("level")]
    public string Level { get; set; } = "low";

    [JsonPropertyName("dictionaryFilterStrategies")]
    public List<DictionaryFilterStrategy>? Strategies { get; set; }
}
