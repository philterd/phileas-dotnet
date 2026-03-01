using System.Text.Json.Serialization;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

/// <summary>
///     Policy configuration for a dictionary-based filter that detects entities by matching
///     against a list of terms, with optional fuzzy matching.
/// </summary>
public class Dictionary : AbstractPolicyFilter
{
    /// <summary>Gets or sets a human-readable name for this dictionary.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the list of terms to detect.</summary>
    [JsonPropertyName("terms")]
    public List<string> Terms { get; set; } = new();

    /// <summary>
    ///     Gets or sets a value indicating whether fuzzy (approximate) matching is enabled. Defaults to
    ///     <see langword="false" />.
    /// </summary>
    [JsonPropertyName("fuzzy")]
    public bool Fuzzy { get; set; } = false;

    /// <summary>
    ///     Gets or sets the fuzzy matching sensitivity level: <c>"low"</c>, <c>"medium"</c>, or <c>"high"</c>. Defaults
    ///     to <c>"low"</c>.
    /// </summary>
    [JsonPropertyName("level")]
    public string Level { get; set; } = "low";

    /// <summary>Gets or sets the list of dictionary filter strategies to apply.</summary>
    [JsonPropertyName("dictionaryFilterStrategies")]
    public List<DictionaryFilterStrategy>? Strategies { get; set; }
}