using System.Text.Json.Serialization;

namespace Phileas.Policy;

public class Graphical
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;

    [JsonPropertyName("redactionColor")]
    public string RedactionColor { get; set; } = "black";
}
