using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Strategies;

namespace Phileas.Strategies.Rules;

/// <summary>
/// Runtime filter strategy for dictionary term detection. Delegates to <see cref="Phileas.Strategies.StandardFilterStrategy.GetStandardReplacement"/> with <c>FilterType.Dictionary</c>.
/// </summary>
public class DictionaryFilterStrategy : StandardFilterStrategy
{
    /// <inheritdoc/>
    public override Replacement GetReplacement(string context, string token, string[] window, double confidence, string? classification, FilterPattern? filterPattern, Crypto? crypto, Fpe? fpe)
        => GetStandardReplacement(context, token, window, confidence, classification, filterPattern, crypto, fpe, FilterType.Dictionary);
}
