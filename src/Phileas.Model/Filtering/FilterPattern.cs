using System.Text.RegularExpressions;

namespace Phileas.Model.Filtering;

public class FilterPattern
{
    public Regex Pattern { get; }
    public string? Format { get; }
    public double InitialConfidence { get; }
    public string? Classification { get; }
    public bool AlwaysValid { get; }
    public int GroupNumber { get; }
    public IList<ConfidenceModifier>? ConfidenceModifiers { get; }

    private FilterPattern(Builder builder)
    {
        Pattern = builder.Pattern ?? throw new ArgumentNullException(nameof(builder.Pattern));
        Format = builder.Format;
        InitialConfidence = builder.InitialConfidence;
        Classification = builder.Classification;
        AlwaysValid = builder.AlwaysValid;
        GroupNumber = builder.GroupNumber;
        ConfidenceModifiers = builder.ConfidenceModifiers;
    }

    public class Builder
    {
        internal Regex? Pattern;
        internal string? Format;
        internal double InitialConfidence = 0.9;
        internal string? Classification;
        internal bool AlwaysValid;
        internal int GroupNumber;
        internal IList<ConfidenceModifier>? ConfidenceModifiers;

        public Builder WithPattern(Regex pattern) { Pattern = pattern; return this; }
        public Builder WithPattern(string pattern, RegexOptions options = RegexOptions.None)
        {
            Pattern = new Regex(pattern, options | RegexOptions.Compiled);
            return this;
        }
        public Builder WithFormat(string format) { Format = format; return this; }
        public Builder WithInitialConfidence(double confidence) { InitialConfidence = confidence; return this; }
        public Builder WithClassification(string classification) { Classification = classification; return this; }
        public Builder WithAlwaysValid(bool alwaysValid) { AlwaysValid = alwaysValid; return this; }
        public Builder WithGroupNumber(int groupNumber) { GroupNumber = groupNumber; return this; }
        public Builder WithConfidenceModifiers(IList<ConfidenceModifier> modifiers) { ConfidenceModifiers = modifiers; return this; }
        public FilterPattern Build() => new FilterPattern(this);
    }
}
