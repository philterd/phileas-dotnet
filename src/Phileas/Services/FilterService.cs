/*
 * Copyright 2026 Philterd, LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Phileas.Filters;
using Phileas.Filters.Rules.Regex;
using Phileas.Filters.Rules.Regex.RegexFilters;
using Phileas.Filters.Strategies.Rules;
using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Services;

/// <summary>
/// Entry-point service for applying a <see cref="Phileas.Policy.Policy"/> to a piece of plain text.
/// </summary>
public class FilterService : IFilterService
{
    /// <inheritdoc/>
    public TextFilterResult Filter(PhileasPolicy policy, string context, int piece, string input, IContextService? contextService = null)
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

    /// <inheritdoc/>
    public EvaluationResult Evaluate(PhileasPolicy policy, string context, int piece, string input, IList<Span> groundTruthSpans, IContextService? contextService = null)
    {
        var result = Filter(policy, context, piece, input, contextService);
        var detectedSpans = result.Spans;

        int truePositives = detectedSpans.Count(d =>
            groundTruthSpans.Any(g => g.CharacterStart == d.CharacterStart && g.CharacterEnd == d.CharacterEnd));

        int falsePositives = detectedSpans.Count - truePositives;
        int falseNegatives = groundTruthSpans.Count(g =>
            !detectedSpans.Any(d => d.CharacterStart == g.CharacterStart && d.CharacterEnd == g.CharacterEnd));

        double precision = (truePositives + falsePositives) > 0
            ? (double)truePositives / (truePositives + falsePositives)
            : 0.0;

        double recall = (truePositives + falseNegatives) > 0
            ? (double)truePositives / (truePositives + falseNegatives)
            : 0.0;

        double f1 = (precision + recall) > 0
            ? 2.0 * precision * recall / (precision + recall)
            : 0.0;

        return new EvaluationResult(precision, recall, f1);
    }

    private IList<AbstractFilter> BuildFilters(PhileasPolicy policy, IContextService contextService)
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

        if (identifiers.PhEyes != null)
        {
            foreach (var phEye in identifiers.PhEyes)
            {
                var strategies = new List<AbstractFilterStrategy>();
                if (phEye.Strategies != null)
                {
                    foreach (var s in phEye.Strategies)
                    {
                        strategies.Add(new PhEyeFilterStrategy
                        {
                            Strategy = s.Strategy,
                            RedactionFormat = s.RedactionFormat,
                            StaticReplacement = s.StaticReplacement ?? string.Empty,
                            MaskCharacter = s.MaskCharacter,
                            MaskLength = s.MaskLength,
                            Condition = s.Condition,
                            Salt = s.Salt,
                            ContextService = contextService
                        });
                    }
                }
                if (strategies.Count == 0)
                    strategies.Add(new PhEyeFilterStrategy { ContextService = contextService });

                var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (phEye.Ignored != null)
                    foreach (var s in phEye.Ignored) ignored.Add(s);

                var config = new FilterConfiguration.Builder()
                    .WithStrategies(strategies)
                    .WithIgnored(ignored)
                    .WithIgnoredPatterns(phEye.IgnoredPatterns ?? new List<IgnoredPattern>())
                    .WithWindowSize(policy.Config.WindowSize)
                    .WithPriority(phEye.Priority)
                    .WithPostFilters(policy.PostFilters)
                    .Build();

                filters.Add(new PhEyeFilter(config, phEye.PhEyeConfiguration, phEye.RemovePunctuation, phEye.Thresholds));
            }
        }

        if (identifiers.Dictionaries != null)
        {
            foreach (var dictionary in identifiers.Dictionaries)
            {
                var strategies = new List<AbstractFilterStrategy>();
                if (dictionary.Strategies != null)
                {
                    foreach (var s in dictionary.Strategies)
                    {
                        strategies.Add(new DictionaryFilterStrategy
                        {
                            Strategy = s.Strategy,
                            RedactionFormat = s.RedactionFormat,
                            StaticReplacement = s.StaticReplacement ?? string.Empty,
                            MaskCharacter = s.MaskCharacter,
                            MaskLength = s.MaskLength,
                            Condition = s.Condition,
                            Salt = s.Salt,
                            ContextService = contextService
                        });
                    }
                }
                if (strategies.Count == 0)
                    strategies.Add(new DictionaryFilterStrategy { ContextService = contextService });

                var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (dictionary.Ignored != null)
                    foreach (var s in dictionary.Ignored) ignored.Add(s);

                var config = new FilterConfiguration.Builder()
                    .WithStrategies(strategies)
                    .WithIgnored(ignored)
                    .WithIgnoredPatterns(dictionary.IgnoredPatterns ?? new List<IgnoredPattern>())
                    .WithWindowSize(policy.Config.WindowSize)
                    .WithPriority(dictionary.Priority)
                    .WithPostFilters(policy.PostFilters)
                    .Build();

                filters.Add(new DictionaryFilter(config, dictionary.Terms, dictionary.Fuzzy, dictionary.Level));
            }
        }

        return filters;
    }

    private TFilter BuildFilter<TFilter, TStrategy>(
        AbstractPolicyFilter policyFilter, PhileasPolicy policy, IContextService contextService)
        where TFilter : RegexFilter
        where TStrategy : AbstractFilterStrategy, new()
    {
        // Extract strategies from the policyFilter using reflection
        var strategiesProperty = policyFilter.GetType().GetProperty("Strategies");
        var strategies = new List<AbstractFilterStrategy>();

        if (strategiesProperty != null)
        {
            var policyStrategies = strategiesProperty.GetValue(policyFilter) as System.Collections.IEnumerable;
            if (policyStrategies != null)
            {
                foreach (var s in policyStrategies)
                {
                    // Copy strategy properties to runtime strategy object
                    var runtimeStrategy = new TStrategy();
                    var sourceType = s.GetType();

                    // Copy all properties from policy strategy to runtime strategy
                    foreach (var prop in sourceType.GetProperties())
                    {
                        var targetProp = typeof(TStrategy).GetProperty(prop.Name);
                        if (targetProp != null && targetProp.CanWrite)
                        {
                            targetProp.SetValue(runtimeStrategy, prop.GetValue(s));
                        }
                    }

                    runtimeStrategy.ContextService = contextService;
                    strategies.Add(runtimeStrategy);
                }
            }
        }

        // If no strategies defined, create a default one
        if (strategies.Count == 0)
        {
            strategies.Add(new TStrategy { ContextService = contextService });
        }

        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (policyFilter.Ignored != null)
            foreach (var s in policyFilter.Ignored) ignored.Add(s);

        var config = new FilterConfiguration.Builder()
            .WithStrategies(strategies)
            .WithIgnored(ignored)
            .WithIgnoredPatterns(policyFilter.IgnoredPatterns ?? new List<IgnoredPattern>())
            .WithCrypto(policy.Crypto)
            .WithFpe(policy.Fpe)
            .WithWindowSize(policy.Config.WindowSize)
            .WithPriority(policyFilter.Priority)
            .WithPostFilters(policy.PostFilters)
            .Build();

        return (TFilter)Activator.CreateInstance(typeof(TFilter), config)!;
    }

    private string ApplyReplacements(string input, IList<Span> spans)
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
