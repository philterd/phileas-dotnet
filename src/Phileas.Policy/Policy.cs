using System.Text.Json.Serialization;

namespace Phileas.Policy;

public class Policy
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("config")]
    public Config Config { get; set; } = new Config();

    [JsonPropertyName("crypto")]
    public Crypto? Crypto { get; set; }

    [JsonPropertyName("fpe")]
    public Fpe? Fpe { get; set; }

    [JsonPropertyName("identifiers")]
    public Identifiers Identifiers { get; set; } = new Identifiers();

    [JsonPropertyName("ignored")]
    public List<Ignored> Ignored { get; set; } = new List<Ignored>();

    [JsonPropertyName("ignoredPatterns")]
    public List<IgnoredPattern> IgnoredPatterns { get; set; } = new List<IgnoredPattern>();

    [JsonPropertyName("graphical")]
    public Graphical Graphical { get; set; } = new Graphical();
}
