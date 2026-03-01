using System.Text.Json.Serialization;

namespace Phileas.Model.Filtering;

public class Span
{
    [JsonPropertyName("characterStart")]
    public int CharacterStart { get; set; }

    [JsonPropertyName("characterEnd")]
    public int CharacterEnd { get; set; }

    [JsonPropertyName("filterType")]
    public FilterType FilterType { get; set; }

    [JsonPropertyName("context")]
    public string Context { get; set; } = string.Empty;

    [JsonPropertyName("classification")]
    public string? Classification { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("replacement")]
    public string Replacement { get; set; } = string.Empty;

    [JsonPropertyName("salt")]
    public string Salt { get; set; } = string.Empty;

    [JsonPropertyName("ignored")]
    public bool Ignored { get; set; }

    [JsonPropertyName("applied")]
    public bool Applied { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonIgnore]
    public string? Pattern { get; set; }

    [JsonIgnore]
    public string[]? Window { get; set; }

    [JsonIgnore]
    public bool AlwaysValid { get; set; }

    public static Span Make(
        int characterStart,
        int characterEnd,
        FilterType filterType,
        string context,
        double confidence,
        string text,
        string replacement,
        string salt,
        bool ignored,
        bool applied,
        string[]? window,
        int priority)
    {
        return new Span
        {
            CharacterStart = characterStart,
            CharacterEnd = characterEnd,
            FilterType = filterType,
            Context = context,
            Confidence = confidence,
            Text = text,
            Replacement = replacement,
            Salt = salt,
            Ignored = ignored,
            Applied = applied,
            Window = window,
            Priority = priority
        };
    }

    public static bool DoesSpanExist(int characterStart, int characterEnd, IList<Span> spans)
    {
        return spans.Any(s => s.CharacterStart == characterStart && s.CharacterEnd == characterEnd);
    }

    public static IList<Span> DropOverlappingSpans(IList<Span> spans)
    {
        var sorted = spans.OrderByDescending(s => s.Confidence).ToList();
        var result = new List<Span>();

        foreach (var span in sorted)
        {
            bool overlaps = result.Any(existing =>
                span.CharacterStart < existing.CharacterEnd &&
                span.CharacterEnd > existing.CharacterStart);

            if (!overlaps)
                result.Add(span);
        }

        return result.OrderBy(s => s.CharacterStart).ToList();
    }
}
