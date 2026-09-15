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

    /// <summary>
    ///     Converts this deprecated entry into the <c>dictionaries</c> form the redaction policy schema
    ///     declares.
    ///     <para>
    ///         <c>name</c> becomes <c>classification</c> and <c>level</c> becomes <c>sensitivity</c>.
    ///         The two spellings share the words <c>low</c>, <c>medium</c> and <c>high</c> but mean
    ///         opposite things, so the mapping goes by what each accepts rather than by name: see
    ///         <see cref="ToSensitivity" />.
    ///     </para>
    /// </summary>
    internal CustomDictionary ToCustomDictionary()
    {
        var converted = new CustomDictionary
        {
            Classification = string.IsNullOrEmpty(Name) ? null : Name,
            Terms = Terms,
            Fuzzy = Fuzzy,
            Sensitivity = ToSensitivity(Level),
            Strategies = Strategies?.Select(CopyStrategy).ToList()
        };

        converted.Enabled = Enabled;
        converted.Ignored = Ignored;
        converted.IgnoredFiles = IgnoredFiles;
        converted.IgnoredPatterns = IgnoredPatterns;
        converted.WindowSize = WindowSize;
        converted.Priority = Priority;

        return converted;
    }

    /// <summary>
    ///     Maps a <c>level</c> to the <c>sensitivity</c> that accepts the same edit distance.
    ///     <para>
    ///         <c>level</c> counted upward (low accepts 1 edit, medium 2, high 3) while
    ///         <c>sensitivity</c> counts downward (high accepts 0, medium 1, low 2), so mapping the
    ///         words onto each other would invert how strict the filter is. <c>level: high</c> has no
    ///         equivalent, since no sensitivity accepts 3 edits; it maps to the loosest available.
    ///     </para>
    /// </summary>
    private static string ToSensitivity(string? level)
    {
        return level?.ToLowerInvariant() switch
        {
            "medium" => "low",
            "high" => "low",
            _ => "medium"
        };
    }

    /// <summary>
    ///     Copies a strategy across. Both types are empty subclasses of the same base, so the base's
    ///     properties are copied wholesale rather than listed, which would silently drop a property
    ///     added to the base later.
    /// </summary>
    private static CustomDictionaryFilterStrategy CopyStrategy(DictionaryFilterStrategy strategy)
    {
        var copy = new CustomDictionaryFilterStrategy();
        foreach (var property in typeof(AbstractFilterStrategy).GetProperties())
            if (property.CanRead && property.CanWrite)
                property.SetValue(copy, property.GetValue(strategy));

        return copy;
    }
}