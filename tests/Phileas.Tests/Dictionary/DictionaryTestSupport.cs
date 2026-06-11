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
using Phileas.Filters.Strategies.Rules;
using Phileas.Model;
using Phileas.Policy.Filters;
using PolicyIdentifiers = Phileas.Policy.Identifiers;
using PhileasPolicy = Phileas.Policy.Policy;
using RuntimeStrategy = Phileas.Filters.AbstractFilterStrategy;
using PCity = Phileas.Policy.Filters.City;
using PStrat = Phileas.Policy.Filters.Strategies;

namespace Phileas.Tests.Dictionaries;

/// <summary>Shared helpers for the dictionary/name/location filter tests (mirrors the Java AbstractFilterTest).</summary>
internal static class DictionaryTestSupport
{
    public const int Piece = 0;
    public const int WindowSize = 3;

    /// <summary>A policy enabling every dictionary/name/location identifier (so HasFilter returns true).</summary>
    public static PhileasPolicy GetPolicy()
    {
        return new PhileasPolicy
        {
            Identifiers = new PolicyIdentifiers
            {
                City = new PCity { Strategies = new List<PStrat.CityFilterStrategy> { new() } },
                County = new County { Strategies = new List<PStrat.CountyFilterStrategy> { new() } },
                State = new State { Strategies = new List<PStrat.StateFilterStrategy> { new() } },
                Hospital = new Hospital { Strategies = new List<PStrat.HospitalFilterStrategy> { new() } },
                FirstName = new FirstName { Strategies = new List<PStrat.FirstNameFilterStrategy> { new() } },
                Surname = new Surname { Strategies = new List<PStrat.SurnameFilterStrategy> { new() } },
                CustomDictionaries = new List<CustomDictionary>
                {
                    new() { Strategies = new List<PStrat.CustomDictionaryFilterStrategy> { new() } }
                }
            }
        };
    }

    /// <summary>A filter configuration with the given runtime strategy (defaults to custom-dictionary).</summary>
    public static FilterConfiguration Config(RuntimeStrategy? strategy = null)
    {
        return new FilterConfiguration.Builder()
            .WithStrategies(new List<RuntimeStrategy> { strategy ?? new CustomDictionaryFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<Phileas.Policy.IgnoredPattern>())
            .WithWindowSize(WindowSize)
            .Build();
    }

    public static bool CheckSpan(Span span, int start, int end, FilterType filterType)
    {
        return span.CharacterStart == start && span.CharacterEnd == end && span.FilterType == filterType;
    }

    public static bool CheckSpanInSpans(IEnumerable<Span> spans, int start, int end, FilterType filterType,
        string text, string replacement)
    {
        return spans.Any(s => s.CharacterStart == start && s.CharacterEnd == end && s.FilterType == filterType
                              && s.Text == text && s.Replacement == replacement);
    }
}
