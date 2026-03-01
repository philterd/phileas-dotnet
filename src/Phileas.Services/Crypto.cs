using System.Text.Json.Serialization;

namespace Phileas.Policy;

public class Crypto
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("iv")]
    public string? Iv { get; set; }
}
