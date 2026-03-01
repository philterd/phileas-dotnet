using System.Text.Json.Serialization;

namespace Phileas.Policy;

public class Fpe
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("tweak")]
    public string? Tweak { get; set; }
}
