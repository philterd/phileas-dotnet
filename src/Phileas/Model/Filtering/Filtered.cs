namespace Phileas.Model.Filtering;

public class Filtered
{
    public string Context { get; }
    public int Piece { get; }
    public IList<Span> Spans { get; }

    public Filtered(string context, IList<Span> spans)
    {
        Context = context;
        Piece = 0;
        Spans = spans;
    }

    public Filtered(string context, int piece, IList<Span> spans)
    {
        Context = context;
        Piece = piece;
        Spans = spans;
    }
}
