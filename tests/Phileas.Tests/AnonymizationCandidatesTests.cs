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
using Phileas.Filters.Rules.Dictionary;
using Phileas.Filters.Rules.Regex.RegexFilters;
using Phileas.Filters.Strategies.Rules;
using Phileas.Model;
using Phileas.Policy.Filters;
using Phileas.Services;
using Xunit;
using static Phileas.Tests.Dictionaries.DictionaryTestSupport;
using PolicyIdentifiers = Phileas.Policy.Identifiers;
using PhileasPolicy = Phileas.Policy.Policy;
using PolicySsnStrategy = Phileas.Policy.Filters.Strategies.SsnFilterStrategy;
using RuntimeStrategy = Phileas.Filters.AbstractFilterStrategy;

namespace Phileas.Tests;

/// <summary>
///     RANDOM_REPLACE with per-strategy anonymization candidates: the replacement is drawn from the
///     strategy's candidate list. Mirrors the Java <c>filterWithCandidates</c> tests.
/// </summary>
public class AnonymizationCandidatesTests
{
    private static readonly List<string> Candidates = new() { "John", "Melissa", "James" };

    private static T WithCandidates<T>(T strategy, List<string> candidates) where T : RuntimeStrategy
    {
        strategy.Strategy = RuntimeStrategy.RandomReplace;
        strategy.AnonymizationCandidates = candidates;
        return strategy;
    }

    [Fact]
    public void FirstName_ReplacementComesFromCandidates()
    {
        var config = Config(WithCandidates(new FirstNameFilterStrategy(), Candidates));
        var filter = new FuzzyDictionaryFilter(FilterType.FirstName, config, SensitivityLevel.Low, true);

        var spans = Span.DropOverlappingSpans(filter.Filter(GetPolicy(), "context", Piece, "Timothy").Spans);

        Assert.Single(spans);
        Assert.Contains(spans[0].Replacement, Candidates);
    }

    [Fact]
    public void FirstName_SingleCandidate()
    {
        var candidates = new List<string> { "John" };
        var config = Config(WithCandidates(new FirstNameFilterStrategy(), candidates));
        var filter = new FuzzyDictionaryFilter(FilterType.FirstName, config, SensitivityLevel.Low, true);

        var spans = Span.DropOverlappingSpans(filter.Filter(GetPolicy(), "context", Piece, "Timothy").Spans);

        Assert.Single(spans);
        Assert.Equal("John", spans[0].Replacement);
    }

    [Fact]
    public void FirstName_EmptyCandidates_StillReplaces()
    {
        var candidates = new List<string>();
        var config = Config(WithCandidates(new FirstNameFilterStrategy(), candidates));
        var filter = new FuzzyDictionaryFilter(FilterType.FirstName, config, SensitivityLevel.Low, true);

        var spans = Span.DropOverlappingSpans(filter.Filter(GetPolicy(), "context", Piece, "Timothy").Spans);

        Assert.Single(spans);
        Assert.NotEmpty(spans[0].Replacement);
        Assert.DoesNotContain(spans[0].Replacement, candidates);
    }

    [Fact]
    public void CustomDictionary_ReplacementComesFromCandidates()
    {
        var candidates = new List<string> { "AAA", "BBB" };
        var config = Config(WithCandidates(new CustomDictionaryFilterStrategy(), candidates));
        var names = new HashSet<string> { "george", "ted", "bill", "john" };
        var filter = new SetDictionaryFilter(FilterType.CustomDictionary, config, names, "none");

        var filtered = filter.Filter(GetPolicy(), "context", Piece, "He lived with Bill in California.");

        Assert.Single(filtered.Spans);
        Assert.Contains(filtered.Spans[0].Replacement, candidates);
    }

    [Fact]
    public void City_ReplacementComesFromCandidates()
    {
        var candidates = new List<string> { "Springfield", "Shelbyville" };
        var config = Config(WithCandidates(new CityFilterStrategy(), candidates));
        var filter = new FuzzyDictionaryFilter(FilterType.LocationCity, config, SensitivityLevel.Low, true);

        var spans = Span.DropOverlappingSpans(filter.Filter(GetPolicy(), "context", Piece, "Lived in Washington.").Spans);

        Assert.NotEmpty(spans);
        Assert.All(spans, s => Assert.Contains(s.Replacement, candidates));
    }

    [Fact]
    public void State_ReplacementComesFromCandidates()
    {
        var candidates = new List<string> { "Ohio", "Iowa" };
        var config = Config(WithCandidates(new StateFilterStrategy(), candidates));
        var filter = new FuzzyDictionaryFilter(FilterType.LocationState, config, SensitivityLevel.Low, true);

        var filtered = filter.Filter(GetPolicy(), "context", Piece, "Lived in Washington");

        Assert.Single(filtered.Spans);
        Assert.Contains(filtered.Spans[0].Replacement, candidates);
    }

    [Fact]
    public void Hospital_ReplacementComesFromCandidates()
    {
        var candidates = new List<string> { "General Hospital" };
        var config = Config(WithCandidates(new HospitalFilterStrategy(), candidates));
        var filter = new FuzzyDictionaryFilter(FilterType.Hospital, config, SensitivityLevel.Low, true);

        var filtered = filter.Filter(GetPolicy(), "context", Piece, "UCLA Medical Center");

        Assert.NotEmpty(filtered.Spans);
        Assert.All(filtered.Spans, s => Assert.Contains(s.Replacement, candidates));
    }

    [Fact]
    public void Surname_ReplacementComesFromCandidates()
    {
        var candidates = new List<string> { "Doe", "Roe" };
        var config = Config(WithCandidates(new SurnameFilterStrategy(), candidates));
        var filter = new FuzzyDictionaryFilter(FilterType.Surname, config, SensitivityLevel.Low, true);

        var spans = Span.DropOverlappingSpans(filter.Filter(GetPolicy(), "context", Piece, "Jones").Spans);

        Assert.Single(spans);
        Assert.Contains(spans[0].Replacement, candidates);
    }

    [Fact]
    public void Identifier_ReplacementComesFromCandidates()
    {
        var candidates = new List<string> { "candidate1", "candidate2" };
        var config = Config(WithCandidates(new IdentifierFilterStrategy(), candidates));
        var filter = new IdentifierFilter(config, "name", Identifier.DefaultIdentifierRegex, true, 0);

        var filtered = filter.Filter(IdentifierSectionTestSupport.GetPolicy(), "context", Piece,
            "the id is AB4736021 in california.");

        Assert.Single(filtered.Spans);
        Assert.Contains(filtered.Spans[0].Replacement, candidates);
    }

    [Fact]
    public void Candidates_FlowFromPolicyThroughFilterService()
    {
        // The policy-level strategy carries the candidates; FilterService copies them onto the runtime
        // strategy and the filter constructor wires a FROM_LIST anonymization service.
        var candidates = new List<string> { "000-00-0000", "111-11-1111" };
        var policy = new PhileasPolicy
        {
            Identifiers = new PolicyIdentifiers
            {
                Ssn = new Ssn
                {
                    Strategies = new List<PolicySsnStrategy>
                    {
                        new()
                        {
                            Strategy = "RANDOM_REPLACE",
                            AnonymizationCandidates = candidates
                        }
                    }
                }
            }
        };

        var result = new FilterService().Filter(policy, "ctx", 0, "SSN: 123-45-6789");

        Assert.Single(result.Spans);
        Assert.Contains(result.Spans[0].Replacement, candidates);
    }
}
