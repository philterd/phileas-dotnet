using System.Text.RegularExpressions;

namespace Phileas.Model.Filtering;

public enum ConfidenceCondition
{
    CharacterSequenceBefore,
    CharacterSequenceAfter,
    CharacterSequenceSurrounding,
    CharacterRegexSurrounding
}

public class ConfidenceModifier
{
    public ConfidenceCondition Condition { get; set; }
    public string Characters { get; set; } = string.Empty;
    public double ConfidenceDelta { get; set; }
    public double Confidence { get; set; }
    public Regex? MatchingPattern { get; set; }
}
