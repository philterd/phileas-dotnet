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

using System.Collections;
using System.Text;
using Phileas.Filters;
using Phileas.Filters.PhEye;
using Phileas.Filters.Rules.Dictionary;
using Phileas.Filters.Rules.Regex;
using Phileas.Filters.Rules.Regex.RegexFilters;
using Phileas.Filters.Strategies;
using Phileas.Filters.Strategies.Rules;
using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services.Anonymization;
using Phileas.Services.Disambiguation;
using Phileas.Services.Generators;
using Phileas.Services.Split;
using Phileas.Services.Tokens;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Services;

/// <summary>
///     Entry-point service for applying a <see cref="Phileas.Policy.Policy" /> to a piece of plain text.
/// </summary>
public class FilterService : IFilterService
{
    /// <summary>The context-window size used when a filter does not specify one of its own.</summary>
    private const int DefaultWindowSize = 5;

    private readonly bool _incrementalRedactionsEnabled;
    private readonly ISpanDisambiguationService _disambiguationService;
    private readonly IContextService? _contextService;
    private static readonly WhitespaceTokenCounter TokenCounter = new();

    /// <summary>Creates a filter service.</summary>
    public FilterService() : this(false)
    {
    }

    /// <summary>Creates a filter service, optionally recording the incremental redaction trail.</summary>
    /// <param name="incrementalRedactionsEnabled">When <see langword="true" />, each result carries a per-redaction snapshot trail.</param>
    public FilterService(bool incrementalRedactionsEnabled)
        : this(incrementalRedactionsEnabled, new NoOpSpanDisambiguationService())
    {
    }

    /// <summary>
    ///     Creates a filter service that uses the supplied <see cref="IContextService" /> for RANDOM_REPLACE
    ///     referential integrity. Inject a durable implementation (e.g. database-backed) so consistent
    ///     replacements persist beyond a single call without the default in-memory store growing unbounded.
    /// </summary>
    /// <param name="contextService">The context service used for all <see cref="Filter" /> calls that don't pass their own.</param>
    public FilterService(IContextService contextService)
        : this(false, new NoOpSpanDisambiguationService(), contextService)
    {
    }

    /// <summary>
    ///     Creates a filter service with span disambiguation. When the supplied service is enabled,
    ///     spans competing at the same location are resolved by surrounding context before overlap
    ///     resolution.
    /// </summary>
    /// <param name="incrementalRedactionsEnabled">When <see langword="true" />, each result carries a per-redaction snapshot trail.</param>
    /// <param name="disambiguationService">The span disambiguation service (use a no-op to disable).</param>
    /// <param name="contextService">
    ///     Optional context service used as the default for <see cref="Filter" /> calls that don't pass
    ///     their own. When null, each <see cref="Filter" /> call falls back to a fresh in-memory store.
    /// </param>
    public FilterService(bool incrementalRedactionsEnabled, ISpanDisambiguationService disambiguationService,
        IContextService? contextService = null)
    {
        _incrementalRedactionsEnabled = incrementalRedactionsEnabled;
        _disambiguationService = disambiguationService;
        _contextService = contextService;
    }

    /// <inheritdoc />
    public TextFilterResult Filter(PhileasPolicy policy, string context, int piece, string input,
        IContextService? contextService = null)
    {
        // Use the per-call service if given, otherwise the injected one, otherwise a fresh in-memory store.
        contextService ??= _contextService ?? new InMemoryContextService();
        var filters = BuildFilters(policy, contextService);

        // Split the input when the policy enables splitting and the document is over the threshold,
        // filter each piece independently, and combine the per-piece results.
        var splitting = policy.Config.Splitting;
        if (splitting.Enabled && input.Length >= splitting.Threshold)
        {
            var splitService = SplitFactory.GetSplitService(splitting.Method, splitting.Threshold);
            var splits = splitService.Split(input);

            var results = new List<TextFilterResult>();
            for (var i = 0; i < splits.Count; i++)
            {
                results.Add(ProcessPiece(policy, filters, context, i, splits[i]));
            }

            return TextFilterResult.Combine(results, context, splitService.GetSeparator());
        }

        return ProcessPiece(policy, filters, context, piece, input);
    }

    private TextFilterResult ProcessPiece(PhileasPolicy policy, IList<AbstractFilter> filters, string context,
        int piece, string input)
    {
        var allSpans = new List<Span>();
        foreach (var filter in filters)
        {
            var filtered = filter.Filter(policy, context, piece, input);
            allSpans.AddRange(filtered.Spans);
        }

        // Resolve spans that compete at the same location (same text classified as different types) using
        // their surrounding context, before overlapping spans are dropped. A no-op service leaves the
        // spans untouched.
        var disambiguatedSpans = _disambiguationService.Disambiguate(context, allSpans);

        var finalSpans = Span.DropOverlappingSpans(disambiguatedSpans);
        finalSpans = ApplyGlobalIgnored(policy, finalSpans);
        var (filteredText, incrementalRedactions) = ApplyReplacements(input, finalSpans);

        return new TextFilterResult(filteredText, context, piece, finalSpans, incrementalRedactions,
            TokenCounter.CountTokens(input));
    }

    /// <summary>
    ///     Applies the policy's document-scoped <c>ignored</c> sets, removing any span whose entity text
    ///     matches an ignored term (or a term loaded from an ignored file). Mirrors the Java
    ///     <c>IgnoredTermsFilter</c> post-filter, which applies regardless of which filter produced the span.
    /// </summary>
    private static IList<Span> ApplyGlobalIgnored(PhileasPolicy policy, IList<Span> spans)
    {
        if (policy.Ignored == null || policy.Ignored.Count == 0)
            return spans;

        var result = spans;
        foreach (var ignored in policy.Ignored)
        {
            var terms = new HashSet<string>();
            foreach (var term in ignored.Terms)
                terms.Add(ignored.CaseSensitive ? term : term.ToLowerInvariant());

            foreach (var file in ignored.Files)
            {
                if (!File.Exists(file))
                    continue;
                foreach (var line in File.ReadAllLines(file))
                    terms.Add(ignored.CaseSensitive ? line : line.ToLowerInvariant());
            }

            if (terms.Count == 0)
                continue;

            result = result.Where(span =>
                !terms.Contains(ignored.CaseSensitive ? span.Text : span.Text.ToLowerInvariant())).ToList();
        }

        return result;
    }

    /// <inheritdoc />
    public EvaluationResult Evaluate(PhileasPolicy policy, string context, int piece, string input,
        IList<Span> groundTruthSpans, IContextService? contextService = null)
    {
        var result = Filter(policy, context, piece, input, contextService);
        var detectedSpans = result.Spans;

        var truePositives = detectedSpans.Count(d =>
            groundTruthSpans.Any(g => g.CharacterStart == d.CharacterStart && g.CharacterEnd == d.CharacterEnd));

        var falsePositives = detectedSpans.Count - truePositives;
        var falseNegatives = groundTruthSpans.Count(g =>
            !detectedSpans.Any(d => d.CharacterStart == g.CharacterStart && d.CharacterEnd == g.CharacterEnd));

        var precision = truePositives + falsePositives > 0
            ? (double)truePositives / (truePositives + falsePositives)
            : 0.0;

        var recall = truePositives + falseNegatives > 0
            ? (double)truePositives / (truePositives + falseNegatives)
            : 0.0;

        var f1 = precision + recall > 0
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
            filters.Add(
                BuildFilter<EmailAddressFilter, EmailAddressFilterStrategy>(identifiers.EmailAddress, policy,
                    contextService));
        if (identifiers.PhoneNumber != null)
            filters.Add(
                BuildFilter<PhoneNumberFilter, PhoneNumberFilterStrategy>(identifiers.PhoneNumber, policy,
                    contextService));
        if (identifiers.Ssn != null)
            filters.Add(BuildFilter<SsnFilter, SsnFilterStrategy>(identifiers.Ssn, policy, contextService));
        if (identifiers.Ein != null)
        {
            var einConfig = BuildRegexConfig<EinFilterStrategy>(identifiers.Ein, policy, contextService);
            filters.Add(new EinFilter(einConfig, identifiers.Ein.OnlyValidPrefixes));
        }
        if (identifiers.ZipCode != null)
        {
            var zipConfig = BuildRegexConfig<ZipCodeFilterStrategy>(identifiers.ZipCode, policy, contextService);
            filters.Add(new ZipCodeFilter(zipConfig, identifiers.ZipCode.RequireDelimiter,
                identifiers.ZipCode.Validate));
        }
        if (identifiers.CreditCard != null)
            filters.Add(
                BuildFilter<CreditCardFilter, CreditCardFilterStrategy>(identifiers.CreditCard, policy,
                    contextService));
        if (identifiers.IpAddress != null)
            filters.Add(
                BuildFilter<IpAddressFilter, IpAddressFilterStrategy>(identifiers.IpAddress, policy, contextService));
        if (identifiers.Url != null)
            filters.Add(BuildFilter<UrlFilter, UrlFilterStrategy>(identifiers.Url, policy, contextService));
        if (identifiers.BitcoinAddress != null)
            filters.Add(BuildFilter<BitcoinAddressFilter, BitcoinAddressFilterStrategy>(identifiers.BitcoinAddress,
                policy, contextService));
        if (identifiers.BankRoutingNumber != null)
            filters.Add(
                BuildFilter<BankRoutingNumberFilter, BankRoutingNumberFilterStrategy>(identifiers.BankRoutingNumber,
                    policy, contextService));
        if (identifiers.MacAddress != null)
            filters.Add(
                BuildFilter<MacAddressFilter, MacAddressFilterStrategy>(identifiers.MacAddress, policy,
                    contextService));
        if (identifiers.Vin != null)
            filters.Add(BuildFilter<VinFilter, VinFilterStrategy>(identifiers.Vin, policy, contextService));
        if (identifiers.Date != null)
        {
            var dateConfig = BuildRegexConfig<DateFilterStrategy>(identifiers.Date, policy, contextService);
            filters.Add(new DateFilter(dateConfig, identifiers.Date.OnlyValidDates));
        }
        if (identifiers.PassportNumber != null)
            filters.Add(BuildFilter<PassportNumberFilter, PassportNumberFilterStrategy>(identifiers.PassportNumber,
                policy, contextService));
        if (identifiers.DriversLicense != null)
            filters.Add(BuildFilter<DriversLicenseFilter, DriversLicenseFilterStrategy>(identifiers.DriversLicense,
                policy, contextService));
        if (identifiers.StreetAddress != null)
            filters.Add(BuildFilter<StreetAddressFilter, StreetAddressFilterStrategy>(identifiers.StreetAddress, policy,
                contextService));
        if (identifiers.PhoneNumberExtension != null)
            filters.Add(
                BuildFilter<PhoneNumberExtensionFilter, PhoneNumberExtensionFilterStrategy>(
                    identifiers.PhoneNumberExtension, policy, contextService));
        if (identifiers.TrackingNumber != null)
            filters.Add(BuildFilter<TrackingNumberFilter, TrackingNumberFilterStrategy>(identifiers.TrackingNumber,
                policy, contextService));
        if (identifiers.IbanCode != null)
            filters.Add(
                BuildFilter<IbanCodeFilter, IbanCodeFilterStrategy>(identifiers.IbanCode, policy, contextService));
        if (identifiers.StateAbbreviation != null)
            filters.Add(
                BuildFilter<StateAbbreviationFilter, StateAbbreviationFilterStrategy>(identifiers.StateAbbreviation,
                    policy, contextService));
        if (identifiers.Currency != null)
            filters.Add(
                BuildFilter<CurrencyFilter, CurrencyFilterStrategy>(identifiers.Currency, policy, contextService));

        if (identifiers.PhEyes != null)
            foreach (var phEye in identifiers.PhEyes)
            {
                var strategies = new List<AbstractFilterStrategy>();
                if (phEye.Strategies != null)
                    foreach (var s in phEye.Strategies)
                        strategies.Add(new PhEyeFilterStrategy
                        {
                            Strategy = s.Strategy,
                            RedactionFormat = s.RedactionFormat,
                            StaticReplacement = s.StaticReplacement ?? string.Empty,
                            MaskCharacter = s.MaskCharacter,
                            MaskLength = s.MaskLength,
                            Condition = s.Condition,
                            Salt = s.Salt,
                            AnonymizationMethod = s.AnonymizationMethod,
                            AnonymizationCandidates = s.AnonymizationCandidates,
                            ReplacementScope = s.ReplacementScope,
                            ContextService = contextService
                        });

                if (strategies.Count == 0)
                    strategies.Add(new PhEyeFilterStrategy { ContextService = contextService });

                var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (phEye.Ignored != null)
                    foreach (var s in phEye.Ignored)
                        ignored.Add(s);

                var config = new FilterConfiguration.Builder()
                    .WithStrategies(strategies)
                    .WithIgnored(ignored)
                    .WithIgnoredPatterns(phEye.IgnoredPatterns ?? new List<IgnoredPattern>())
                    .WithWindowSize(phEye.GetWindowSizeOrDefault(DefaultWindowSize))
                    .WithPriority(phEye.Priority)
                    .WithPostFilters(policy.Config.PostFilters)
                    .Build();

                filters.Add(
                    new PhEyeFilter(config, phEye.PhEyeConfiguration, phEye.RemovePunctuation, phEye.Thresholds));
            }

        if (identifiers.Dictionaries != null)
            foreach (var dictionary in identifiers.Dictionaries)
            {
                var strategies = new List<AbstractFilterStrategy>();
                if (dictionary.Strategies != null)
                    foreach (var s in dictionary.Strategies)
                        strategies.Add(new DictionaryFilterStrategy
                        {
                            Strategy = s.Strategy,
                            RedactionFormat = s.RedactionFormat,
                            StaticReplacement = s.StaticReplacement ?? string.Empty,
                            MaskCharacter = s.MaskCharacter,
                            MaskLength = s.MaskLength,
                            Condition = s.Condition,
                            Salt = s.Salt,
                            AnonymizationMethod = s.AnonymizationMethod,
                            AnonymizationCandidates = s.AnonymizationCandidates,
                            ReplacementScope = s.ReplacementScope,
                            ContextService = contextService
                        });

                if (strategies.Count == 0)
                    strategies.Add(new DictionaryFilterStrategy { ContextService = contextService });

                var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (dictionary.Ignored != null)
                    foreach (var s in dictionary.Ignored)
                        ignored.Add(s);

                var config = new FilterConfiguration.Builder()
                    .WithStrategies(strategies)
                    .WithIgnored(ignored)
                    .WithIgnoredPatterns(dictionary.IgnoredPatterns ?? new List<IgnoredPattern>())
                    .WithWindowSize(dictionary.GetWindowSizeOrDefault(DefaultWindowSize))
                    .WithPriority(dictionary.Priority)
                    .WithPostFilters(policy.Config.PostFilters)
                    .Build();

                filters.Add(new DictionaryFilter(config, dictionary.Terms, dictionary.Fuzzy, dictionary.Level));
            }

        // Dictionary-backed name/location filters (load the bundled term lists by filter type).
        if (identifiers.City != null)
            filters.Add(BuildDictionaryFilter(identifiers.City, identifiers.City.Strategies, FilterType.LocationCity,
                identifiers.City.Fuzzy, identifiers.City.Sensitivity, identifiers.City.Capitalized, policy, contextService));
        if (identifiers.County != null)
            filters.Add(BuildDictionaryFilter(identifiers.County, identifiers.County.Strategies, FilterType.LocationCounty,
                identifiers.County.Fuzzy, identifiers.County.Sensitivity, identifiers.County.Capitalized, policy, contextService));
        if (identifiers.State != null)
            filters.Add(BuildDictionaryFilter(identifiers.State, identifiers.State.Strategies, FilterType.LocationState,
                identifiers.State.Fuzzy, identifiers.State.Sensitivity, identifiers.State.Capitalized, policy, contextService));
        if (identifiers.Hospital != null)
            filters.Add(BuildDictionaryFilter(identifiers.Hospital, identifiers.Hospital.Strategies, FilterType.Hospital,
                identifiers.Hospital.Fuzzy, identifiers.Hospital.Sensitivity, identifiers.Hospital.Capitalized, policy, contextService));
        if (identifiers.FirstName != null)
            filters.Add(BuildDictionaryFilter(identifiers.FirstName, identifiers.FirstName.Strategies, FilterType.FirstName,
                identifiers.FirstName.Fuzzy, identifiers.FirstName.Sensitivity, identifiers.FirstName.Capitalized, policy, contextService));
        if (identifiers.Surname != null)
            filters.Add(BuildDictionaryFilter(identifiers.Surname, identifiers.Surname.Strategies, FilterType.Surname,
                identifiers.Surname.Fuzzy, identifiers.Surname.Sensitivity, identifiers.Surname.Capitalized, policy, contextService));

        if (identifiers.CustomDictionaries != null)
            foreach (var customDictionary in identifiers.CustomDictionaries)
                filters.Add(BuildCustomDictionaryFilter(customDictionary, policy, contextService));

        // Custom regex identifier filters.
        if (identifiers.CustomIdentifiers != null)
            foreach (var identifier in identifiers.CustomIdentifiers)
            {
                var config = BuildDictionaryConfig(identifier, identifier.Strategies, FilterType.Identifier, policy,
                    contextService);
                var validator = Validators.IdentifierValidators.FromPolicy(identifier.Validator);
                filters.Add(new IdentifierFilter(config, identifier.Classification, identifier.Pattern,
                    identifier.CaseSensitive, identifier.GroupNumber, validator));
            }

        // Section filters.
        if (identifiers.Sections != null)
            foreach (var section in identifiers.Sections)
            {
                var config = BuildDictionaryConfig(section, section.Strategies, FilterType.Section, policy,
                    contextService);
                filters.Add(new SectionFilter(config, section.StartPattern ?? string.Empty,
                    section.EndPattern ?? string.Empty));
            }

        WireReplacementValidators(policy, filters);

        return filters;
    }

    /// <summary>
    ///     Injects a re-scan validator into every MAP_REPLACE strategy once the full filter set is built. The validator
    ///     runs all filters over a generated value to reject one that reintroduces PII. A no-op when no MAP_REPLACE
    ///     strategy is present.
    /// </summary>
    private static void WireReplacementValidators(PhileasPolicy policy, IList<AbstractFilter> filters)
    {
        IReplacementValidator? validator = null;

        foreach (var filter in filters)
            foreach (var strategy in filter.GetStrategies())
                if (strategy is StandardFilterStrategy standard
                    && string.Equals(standard.Strategy, AbstractFilterStrategy.MapReplace,
                        StringComparison.OrdinalIgnoreCase))
                {
                    validator ??= new PipelineReplacementValidator(policy, filters);
                    standard.ReplacementValidator = validator;
                }
    }

    private AbstractFilter BuildDictionaryFilter(AbstractPolicyFilter policyFilter,
        IEnumerable<Policy.Filters.Strategies.AbstractFilterStrategy>? policyStrategies, FilterType filterType,
        bool fuzzy, string sensitivity, bool capitalized, PhileasPolicy policy, IContextService contextService)
    {
        var config = BuildDictionaryConfig(policyFilter, policyStrategies, filterType, policy, contextService);
        return fuzzy
            ? new FuzzyDictionaryFilter(filterType, config, SensitivityLevels.FromName(sensitivity), capitalized)
            : new SetDictionaryFilter(filterType, config);
    }

    private AbstractFilter BuildCustomDictionaryFilter(Policy.Filters.CustomDictionary customDictionary,
        PhileasPolicy policy, IContextService contextService)
    {
        var config = BuildDictionaryConfig(customDictionary, customDictionary.Strategies, FilterType.CustomDictionary,
            policy, contextService);
        var terms = customDictionary.Terms ?? new List<string>();
        return customDictionary.Fuzzy
            ? new FuzzyDictionaryFilter(FilterType.CustomDictionary, config,
                SensitivityLevels.FromName(customDictionary.Sensitivity), terms, customDictionary.Capitalized)
            : new SetDictionaryFilter(FilterType.CustomDictionary, config, terms, customDictionary.Classification);
    }

    private FilterConfiguration BuildDictionaryConfig(AbstractPolicyFilter policyFilter,
        IEnumerable<Policy.Filters.Strategies.AbstractFilterStrategy>? policyStrategies, FilterType filterType,
        PhileasPolicy policy, IContextService contextService)
    {
        var strategies = new List<AbstractFilterStrategy>();
        if (policyStrategies != null)
            foreach (var s in policyStrategies)
            {
                var runtimeStrategy = CreateDictionaryRuntimeStrategy(filterType);
                runtimeStrategy.Strategy = s.Strategy;
                runtimeStrategy.RedactionFormat = s.RedactionFormat;
                runtimeStrategy.StaticReplacement = s.StaticReplacement ?? string.Empty;
                runtimeStrategy.MaskCharacter = s.MaskCharacter;
                runtimeStrategy.MaskLength = s.MaskLength;
                runtimeStrategy.Condition = s.Condition;
                runtimeStrategy.Salt = s.Salt;
                runtimeStrategy.AnonymizationMethod = s.AnonymizationMethod;
                runtimeStrategy.AnonymizationCandidates = s.AnonymizationCandidates;
                runtimeStrategy.ReplacementScope = s.ReplacementScope;
                runtimeStrategy.ContextService = contextService;
                strategies.Add(runtimeStrategy);
            }

        if (strategies.Count == 0)
        {
            var runtimeStrategy = CreateDictionaryRuntimeStrategy(filterType);
            runtimeStrategy.ContextService = contextService;
            strategies.Add(runtimeStrategy);
        }

        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (policyFilter.Ignored != null)
            foreach (var s in policyFilter.Ignored)
                ignored.Add(s);

        var config = new FilterConfiguration.Builder()
            .WithStrategies(strategies)
            .WithIgnored(ignored)
            .WithIgnoredPatterns(policyFilter.IgnoredPatterns ?? new List<IgnoredPattern>())
            .WithWindowSize(policyFilter.GetWindowSizeOrDefault(DefaultWindowSize))
            .WithPriority(policyFilter.Priority)
            .WithPostFilters(policy.Config.PostFilters)
            .Build();

        return config;
    }

    private static AbstractFilterStrategy CreateDictionaryRuntimeStrategy(FilterType filterType)
    {
        return filterType switch
        {
            FilterType.LocationCity => new Filters.Strategies.Rules.CityFilterStrategy(),
            FilterType.LocationCounty => new Filters.Strategies.Rules.CountyFilterStrategy(),
            FilterType.LocationState => new Filters.Strategies.Rules.StateFilterStrategy(),
            FilterType.Hospital => new Filters.Strategies.Rules.HospitalFilterStrategy(),
            FilterType.FirstName => new Filters.Strategies.Rules.FirstNameFilterStrategy(),
            FilterType.Surname => new Filters.Strategies.Rules.SurnameFilterStrategy(),
            FilterType.Identifier => new Filters.Strategies.Rules.IdentifierFilterStrategy(),
            FilterType.Section => new Filters.Strategies.Rules.SectionFilterStrategy(),
            _ => new Filters.Strategies.Rules.CustomDictionaryFilterStrategy()
        };
    }

    private TFilter BuildFilter<TFilter, TStrategy>(
        AbstractPolicyFilter policyFilter, PhileasPolicy policy, IContextService contextService)
        where TFilter : RegexFilter
        where TStrategy : AbstractFilterStrategy, new()
    {
        var config = BuildRegexConfig<TStrategy>(policyFilter, policy, contextService);
        return (TFilter)Activator.CreateInstance(typeof(TFilter), config)!;
    }

    private FilterConfiguration BuildRegexConfig<TStrategy>(
        AbstractPolicyFilter policyFilter, PhileasPolicy policy, IContextService contextService)
        where TStrategy : AbstractFilterStrategy, new()
    {
        // Extract strategies from the policyFilter using reflection
        var strategiesProperty = policyFilter.GetType().GetProperty("Strategies");
        var strategies = new List<AbstractFilterStrategy>();

        if (strategiesProperty != null)
        {
            var policyStrategies = strategiesProperty.GetValue(policyFilter) as IEnumerable;
            if (policyStrategies != null)
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
                            targetProp.SetValue(runtimeStrategy, prop.GetValue(s));
                    }

                    runtimeStrategy.ContextService = contextService;
                    ResolveMapReplace(runtimeStrategy, policy);
                    strategies.Add(runtimeStrategy);
                }
        }

        // If no strategies defined, create a default one
        if (strategies.Count == 0) strategies.Add(new TStrategy { ContextService = contextService });

        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (policyFilter.Ignored != null)
            foreach (var s in policyFilter.Ignored)
                ignored.Add(s);

        return new FilterConfiguration.Builder()
            .WithStrategies(strategies)
            .WithIgnored(ignored)
            .WithIgnoredPatterns(policyFilter.IgnoredPatterns ?? new List<IgnoredPattern>())
            .WithCrypto(policy.Crypto)
            .WithFpe(policy.Fpe)
            .WithWindowSize(policyFilter.GetWindowSizeOrDefault(DefaultWindowSize))
            .WithPriority(policyFilter.Priority)
            .WithPostFilters(policy.Config.PostFilters)
            .Build();
    }

    // Shared across generator calls; per-call timeouts are enforced with a CancellationToken in the generator.
    private static readonly HttpClient GeneratorHttpClient = new();

    /// <summary>
    ///     Wires a MAP_REPLACE runtime strategy: builds its lookup table (merging any TSV mapping files with the inline
    ///     mappings) and resolves its generator reference against the policy's <c>generators</c> block. A no-op for any
    ///     other strategy.
    /// </summary>
    private static void ResolveMapReplace(AbstractFilterStrategy strategy, PhileasPolicy policy)
    {
        if (strategy is not StandardFilterStrategy standard) return;
        if (!string.Equals(standard.Strategy, AbstractFilterStrategy.MapReplace, StringComparison.OrdinalIgnoreCase))
            return;

        standard.InitializeMappings(LoadMappingFiles(standard.MappingFiles));

        var generatorName = standard.Generator;
        if (string.IsNullOrEmpty(generatorName)) return;

        if (policy.Generators == null
            || !policy.Generators.TryGetValue(generatorName, out var generator)
            || generator == null)
            // The strategy references a generator that is not defined; it will use its fallback strategy.
            return;

        if (string.Equals(generator.Type, Generator.TypeOllama, StringComparison.OrdinalIgnoreCase))
            standard.ReplacementGenerator = new OllamaReplacementGenerator(generator, GeneratorHttpClient);
        // An unsupported generator type is ignored; the strategy uses its fallback strategy.
    }

    /// <summary>
    ///     Loads the MAP_REPLACE mapping files into a single lookup table. Each file is a TSV with one tab-delimited
    ///     key/value pair per row; a row without a tab is skipped. Later files override earlier ones for a duplicate
    ///     key; inline mappings later override the merged file entries.
    /// </summary>
    private static Dictionary<string, string> LoadMappingFiles(List<string>? mappingFiles)
    {
        var loaded = new Dictionary<string, string>();
        if (mappingFiles == null) return loaded;

        foreach (var fileName in mappingFiles)
        {
            if (!File.Exists(fileName)) continue;

            try
            {
                foreach (var line in File.ReadLines(fileName))
                {
                    if (line.Length == 0) continue;
                    var tab = line.IndexOf('\t');
                    // A row without a tab has no value; skip it rather than mapping to an empty string.
                    if (tab < 0) continue;
                    loaded[line[..tab]] = line[(tab + 1)..];
                }
            }
            catch (IOException)
            {
                // An unreadable mapping file is skipped; the strategy still applies inline mappings and its fallback.
            }
        }

        return loaded;
    }


    private (string FilteredText, IList<IncrementalRedaction> IncrementalRedactions) ApplyReplacements(
        string input, IList<Span> spans)
    {
        var incrementalRedactions = new List<IncrementalRedaction>();
        if (!spans.Any()) return (input, incrementalRedactions);

        // Apply the replacements in ascending start order, tracking the cumulative offset introduced by
        // replacements whose length differs from the original span. The spans do not overlap, so this
        // single left-to-right pass is safe.
        var sb = new StringBuilder(input);
        var offset = 0;

        foreach (var span in spans.OrderBy(s => s.CharacterStart))
        {
            var start = span.CharacterStart + offset;
            var length = span.CharacterEnd - span.CharacterStart;
            sb.Remove(start, length);
            sb.Insert(start, span.Replacement);
            offset += span.Replacement.Length - length;

            if (_incrementalRedactionsEnabled)
            {
                // Hash the document as it stands after this redaction.
                var snapshot = sb.ToString();
                incrementalRedactions.Add(new IncrementalRedaction(Sha256Hex(snapshot), span, snapshot));
            }
        }

        return (sb.ToString(), incrementalRedactions);
    }

    private static string Sha256Hex(string text)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}