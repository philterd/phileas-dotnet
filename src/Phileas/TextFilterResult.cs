using Phileas.Model.Filtering;

namespace Phileas;

public class TextFilterResult
{
    public string FilteredText { get; }
    public IList<Span> Spans { get; }

    public TextFilterResult(string filteredText, IList<Span> spans)
    {
        FilteredText = filteredText;
        Spans = spans;
    }
}
