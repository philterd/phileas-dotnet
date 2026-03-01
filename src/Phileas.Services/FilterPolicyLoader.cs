using Phileas.Filters;
using Phileas.Filters.Rules.Regex;
using Phileas.Model.Filtering;
using Phileas.Policy;
using PhileasPolicy = Phileas.Policy.Policy;
using Phileas.Services.Filters.Regex;
using Phileas.Services.Strategies.Rules;

namespace Phileas.Services;

public static class FilterPolicyLoader
{
    public static TextFilterResult Filter(IList<PhileasPolicy> policies, string context, int piece, string input)
    {
        var allSpans = new List<Span>();

        foreach (var policy in policies)
        {
            var filters = BuildFilters(policy);
            foreach (var filter in filters)
            {
                var filtered = filter.Filter(policy, context, piece, input);
                allSpans.AddRange(filtered.Spans);
            }
        }

        var finalSpans = Span.DropOverlappingSpans(allSpans);
        var filteredText = ApplyReplacements(input, finalSpans);

        return new TextFilterResult(filteredText, finalSpans);
    }

    private static IList<AbstractFilter> BuildFilters(PhileasPolicy policy)
    {
        var filters = new List<AbstractFilter>();
        var identifiers = policy.Identifiers;

        if (identifiers.Age != null)
            filters.Add(BuildFilter<AgeFilter, AgeFilterStrategy>(identifiers.Age, policy));
        if (identifiers.EmailAddress != null)
            filters.Add(BuildFilter<EmailAddressFilter, EmailAddressFilterStrategy>(identifiers.EmailAddress, policy));
        if (identifiers.PhoneNumber != null)
            filters.Add(BuildFilter<PhoneNumberFilter, PhoneNumberFilterStrategy>(identifiers.PhoneNumber, policy));
        if (identifiers.Ssn != null)
            filters.Add(BuildFilter<SsnFilter, SsnFilterStrategy>(identifiers.Ssn, policy));
        if (identifiers.ZipCode != null)
            filters.Add(BuildFilter<ZipCodeFilter, ZipCodeFilterStrategy>(identifiers.ZipCode, policy));
        if (identifiers.CreditCard != null)
            filters.Add(BuildFilter<CreditCardFilter, CreditCardFilterStrategy>(identifiers.CreditCard, policy));
        if (identifiers.IpAddress != null)
            filters.Add(BuildFilter<IpAddressFilter, IpAddressFilterStrategy>(identifiers.IpAddress, policy));
        if (identifiers.Url != null)
            filters.Add(BuildFilter<UrlFilter, UrlFilterStrategy>(identifiers.Url, policy));
        if (identifiers.BitcoinAddress != null)
            filters.Add(BuildFilter<BitcoinAddressFilter, BitcoinAddressFilterStrategy>(identifiers.BitcoinAddress, policy));
        if (identifiers.BankRoutingNumber != null)
            filters.Add(BuildFilter<BankRoutingNumberFilter, BankRoutingNumberFilterStrategy>(identifiers.BankRoutingNumber, policy));
        if (identifiers.MacAddress != null)
            filters.Add(BuildFilter<MacAddressFilter, MacAddressFilterStrategy>(identifiers.MacAddress, policy));
        if (identifiers.Vin != null)
            filters.Add(BuildFilter<VinFilter, VinFilterStrategy>(identifiers.Vin, policy));
        if (identifiers.Date != null)
            filters.Add(BuildFilter<DateFilter, DateFilterStrategy>(identifiers.Date, policy));
        if (identifiers.PassportNumber != null)
            filters.Add(BuildFilter<PassportNumberFilter, PassportNumberFilterStrategy>(identifiers.PassportNumber, policy));
        if (identifiers.DriversLicense != null)
            filters.Add(BuildFilter<DriversLicenseFilter, DriversLicenseFilterStrategy>(identifiers.DriversLicense, policy));
        if (identifiers.StreetAddress != null)
            filters.Add(BuildFilter<StreetAddressFilter, StreetAddressFilterStrategy>(identifiers.StreetAddress, policy));
        if (identifiers.PhoneNumberExtension != null)
            filters.Add(BuildFilter<PhoneNumberExtensionFilter, PhoneNumberExtensionFilterStrategy>(identifiers.PhoneNumberExtension, policy));
        if (identifiers.TrackingNumber != null)
            filters.Add(BuildFilter<TrackingNumberFilter, TrackingNumberFilterStrategy>(identifiers.TrackingNumber, policy));
        if (identifiers.IbanCode != null)
            filters.Add(BuildFilter<IbanCodeFilter, IbanCodeFilterStrategy>(identifiers.IbanCode, policy));
        if (identifiers.StateAbbreviation != null)
            filters.Add(BuildFilter<StateAbbreviationFilter, StateAbbreviationFilterStrategy>(identifiers.StateAbbreviation, policy));
        if (identifiers.Currency != null)
            filters.Add(BuildFilter<CurrencyFilter, CurrencyFilterStrategy>(identifiers.Currency, policy));

        return filters;
    }

    private static TFilter BuildFilter<TFilter, TStrategy>(
        Phileas.Policy.Filters.AbstractPolicyFilter policyFilter, PhileasPolicy policy)
        where TFilter : RegexFilter
        where TStrategy : AbstractFilterStrategy, new()
    {
        var strategy = new TStrategy();
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
