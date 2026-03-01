using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Strategies;

namespace Phileas.Strategies.Rules;

public class PhoneNumberFilterStrategy : StandardFilterStrategy
{
    public override Replacement GetReplacement(string context, string token, string[] window, double confidence, string? classification, FilterPattern? filterPattern, Crypto? crypto, Fpe? fpe)
        => GetStandardReplacement(context, token, window, confidence, classification, filterPattern, crypto, fpe, FilterType.PhoneNumber);
}
