using System.Text.Json.Serialization;

namespace Phileas.Policy;

public class IgnoredPattern
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("caseSensitive")]
    public bool CaseSensitive { get; set; } = false;
}
