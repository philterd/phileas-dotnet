using Phileas.Filters;
using Phileas.Filters.Rules.Regex;
using Phileas.Model.Filtering;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;
using Phileas.Filters.Regex;
using Phileas.Strategies.Rules;

namespace Phileas;

public static class FilterPolicyLoader
{
    public static TextFilterResult Filter(PhileasPolicy policy, string context, int piece, string input, IContextService? contextService = null)
    {
        contextService ??= new InMemoryContextService();
        var allSpans = new List<Span>();

        var filters = BuildFilters(policy, contextService);
        foreach (var filter in filters)
        {
            var filtered = filter.Filter(policy, context, piece, input);
            allSpans.AddRange(filtered.Spans);
        }

        var finalSpans = Span.DropOverlappingSpans(allSpans);
        var filteredText = ApplyReplacements(input, finalSpans);

        return new TextFilterResult(filteredText, finalSpans);
    }

    private static IList<AbstractFilter> BuildFilters(PhileasPolicy policy, IContextService contextService)
    {
        var filters = new List<AbstractFilter>();
        var identifiers = policy.Identifiers;

        if (identifiers.Age != null)
            filters.Add(BuildFilter<AgeFilter, AgeFilterStrategy>(identifiers.Age, policy, contextService));
        if (identifiers.EmailAddress != null)
            filters.Add(BuildFilter<EmailAddressFilter, EmailAddressFilterStrategy>(identifiers.EmailAddress, policy, contextService));
        if (identifiers.PhoneNumber != null)
            filters.Add(BuildFilter<PhoneNumberFilter, PhoneNumberFilterStrategy>(identifiers.PhoneNumber, policy, contextService));
        if (identifiers.Ssn != null)
            filters.Add(BuildFilter<SsnFilter, SsnFilterStrategy>(identifiers.Ssn, policy, contextService));
        if (identifiers.ZipCode != null)
            filters.Add(BuildFilter<ZipCodeFilter, ZipCodeFilterStrategy>(identifiers.ZipCode, policy, contextService));
        if (identifiers.CreditCard != null)
            filters.Add(BuildFilter<CreditCardFilter, CreditCardFilterStrategy>(identifiers.CreditCard, policy, contextService));
        if (identifiers.IpAddress != null)
            filters.Add(BuildFilter<IpAddressFilter, IpAddressFilterStrategy>(identifiers.IpAddress, policy, contextService));
        if (identifiers.Url != null)
            filters.Add(BuildFilter<UrlFilter, UrlFilterStrategy>(identifiers.Url, policy, contextService));
        if (identifiers.BitcoinAddress != null)
            filters.Add(BuildFilter<BitcoinAddressFilter, BitcoinAddressFilterStrategy>(identifiers.BitcoinAddress, policy, contextService));
        if (identifiers.BankRoutingNumber != null)
            filters.Add(BuildFilter<BankRoutingNumberFilter, BankRoutingNumberFilterStrategy>(identifiers.BankRoutingNumber, policy, contextService));
        if (identifiers.MacAddress != null)
            filters.Add(BuildFilter<MacAddressFilter, MacAddressFilterStrategy>(identifiers.MacAddress, policy, contextService));
        if (identifiers.Vin != null)
            filters.Add(BuildFilter<VinFilter, VinFilterStrategy>(identifiers.Vin, policy, contextService));
        if (identifiers.Date != null)
            filters.Add(BuildFilter<DateFilter, DateFilterStrategy>(identifiers.Date, policy, contextService));
        if (identifiers.PassportNumber != null)
            filters.Add(BuildFilter<PassportNumberFilter, PassportNumberFilterStrategy>(identifiers.PassportNumber, policy, contextService));
        if (identifiers.DriversLicense != null)
            filters.Add(BuildFilter<DriversLicenseFilter, DriversLicenseFilterStrategy>(identifiers.DriversLicense, policy, contextService));
        if (identifiers.StreetAddress != null)
            filters.Add(BuildFilter<StreetAddressFilter, StreetAddressFilterStrategy>(identifiers.StreetAddress, policy, contextService));
        if (identifiers.PhoneNumberExtension != null)
            filters.Add(BuildFilter<PhoneNumberExtensionFilter, PhoneNumberExtensionFilterStrategy>(identifiers.PhoneNumberExtension, policy, contextService));
        if (identifiers.TrackingNumber != null)
            filters.Add(BuildFilter<TrackingNumberFilter, TrackingNumberFilterStrategy>(identifiers.TrackingNumber, policy, contextService));
        if (identifiers.IbanCode != null)
            filters.Add(BuildFilter<IbanCodeFilter, IbanCodeFilterStrategy>(identifiers.IbanCode, policy, contextService));
        if (identifiers.StateAbbreviation != null)
            filters.Add(BuildFilter<StateAbbreviationFilter, StateAbbreviationFilterStrategy>(identifiers.StateAbbreviation, policy, contextService));
        if (identifiers.Currency != null)
            filters.Add(BuildFilter<CurrencyFilter, CurrencyFilterStrategy>(identifiers.Currency, policy, contextService));

        return filters;
    }

    private static TFilter BuildFilter<TFilter, TStrategy>(
        Phileas.Policy.Filters.AbstractPolicyFilter policyFilter, PhileasPolicy policy, IContextService contextService)
        where TFilter : RegexFilter
        where TStrategy : AbstractFilterStrategy, new()
    {
        var strategy = new TStrategy { ContextService = contextService };
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (policyFilter.Ignored != null)
            foreach (var s in policyFilter.Ignored) ignored.Add(s);

        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { strategy })
            .WithIgnored(ignored)
            .WithIgnoredPatterns(policyFilter.IgnoredPatterns ?? new List<IgnoredPattern>())
            .WithWindowSize(policy.Config.WindowSize)
            .WithPriority(policyFilter.Priority)
            .Build();

        return (TFilter)Activator.CreateInstance(typeof(TFilter), config)!;
    }

    private static string ApplyReplacements(string input, IList<Span> spans)
    {
        if (!spans.Any()) return input;

        var result = new System.Text.StringBuilder();
        int lastIndex = 0;

        foreach (var span in spans.OrderBy(s => s.CharacterStart))
        {
            if (span.CharacterStart > lastIndex)
                result.Append(input, lastIndex, span.CharacterStart - lastIndex);
            result.Append(span.Replacement);
            lastIndex = span.CharacterEnd;
        }

        if (lastIndex < input.Length)
            result.Append(input, lastIndex, input.Length - lastIndex);

        return result.ToString();
    }
}
