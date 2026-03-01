using Phileas.Model;
using Phileas.Policy;

namespace Phileas.Filters.Strategies.Rules;

/// <summary>
///     Runtime filter strategy for dictionary term detection. Delegates to
///     <see cref="Phileas.Filters.Strategies.StandardFilterStrategy" /> with <c>FilterType.Dictionary</c>.
/// </summary>
public class DictionaryFilterStrategy : StandardFilterStrategy
{
    /// <inheritdoc />
    public override Replacement GetReplacement(string context, string token, string[] window, double confidence,
        string? classification, FilterPattern? filterPattern, Crypto? crypto, Fpe? fpe)
    {
        return GetStandardReplacement(context, token, window, confidence, classification, filterPattern, crypto, fpe,
            FilterType.Dictionary);
    }
}