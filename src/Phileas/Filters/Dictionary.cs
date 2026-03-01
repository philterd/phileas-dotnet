using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

public class Dictionary : AbstractPolicyFilter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("terms")]
    public List<string> Terms { get; set; } = new List<string>();

    [JsonPropertyName("dictionaryFilterStrategies")]
    public List<DictionaryFilterStrategy>? Strategies { get; set; }
}
