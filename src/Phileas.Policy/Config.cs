using System.Text.Json.Serialization;

namespace Phileas.Policy;

public class Config
{
    [JsonPropertyName("windowSize")]
    public int WindowSize { get; set; } = 5;

    [JsonPropertyName("splitOnPunctuation")]
    public bool SplitOnPunctuation { get; set; } = false;
}
