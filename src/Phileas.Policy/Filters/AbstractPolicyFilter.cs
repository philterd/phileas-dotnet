using System.Text.Json.Serialization;

namespace Phileas.Policy.Filters;

public abstract class AbstractPolicyFilter
{
    [JsonPropertyName("ignored")]
    public List<string>? Ignored { get; set; }

    [JsonPropertyName("ignoredFiles")]
    public List<string>? IgnoredFiles { get; set; }

    [JsonPropertyName("ignoredPatterns")]
    public List<IgnoredPattern>? IgnoredPatterns { get; set; }

    [JsonPropertyName("sensitivity")]
    public string Sensitivity { get; set; } = "medium";

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 0;
}
