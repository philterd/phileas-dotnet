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

namespace Phileas.Data.Generators;

/// <summary>Selects a random US state abbreviation.</summary>
public class StateAbbreviationGenerator : IGenerator<string>
{
    private static readonly List<string> DefaultAbbreviations = new()
    {
        "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA", "HI", "ID", "IL", "IN", "IA", "KS",
        "KY", "LA", "ME", "MD", "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ", "NM", "NY",
        "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC", "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV",
        "WI", "WY"
    };

    private readonly Random _random;
    private readonly List<string> _abbreviations;

    public StateAbbreviationGenerator(Random random) : this(random, DefaultAbbreviations) { }

    public StateAbbreviationGenerator(Random random, List<string> abbreviations)
    {
        _random = random;
        _abbreviations = abbreviations;
    }

    public string Random() => _abbreviations[_random.Next(_abbreviations.Count)];

    public long PoolSize() => _abbreviations.Count;
}
