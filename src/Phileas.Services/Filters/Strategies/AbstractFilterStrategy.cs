using System.Text.Json.Serialization;

namespace Phileas.Policy.Filters.Strategies;

public abstract class AbstractFilterStrategy
{
    [JsonPropertyName("strategy")]
    public string Strategy { get; set; } = "REDACT";

    [JsonPropertyName("redactionFormat")]
    public string RedactionFormat { get; set; } = "{{{REDACTED-%t}}}";

    [JsonPropertyName("staticReplacement")]
    public string? StaticReplacement { get; set; }

    [JsonPropertyName("maskCharacter")]
    public string MaskCharacter { get; set; } = "*";

    [JsonPropertyName("maskLength")]
    public string MaskLength { get; set; } = "same";

    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    [JsonPropertyName("salt")]
    public bool Salt { get; set; } = false;
}
