using System.Text.Json.Serialization;

namespace Phileas.Policy;

public class Ignored
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("caseSensitive")]
    public bool CaseSensitive { get; set; } = false;
}
