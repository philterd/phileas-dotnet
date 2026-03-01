namespace Phileas.Model.Filtering;

public class Replacement
{
    public string Value { get; }
    public string Salt { get; }
    public bool Applied { get; }

    public Replacement(string value, string salt, bool applied = true)
    {
        Value = value;
        Salt = salt;
        Applied = applied;
    }
}
