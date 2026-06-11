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

/// <summary>Selects a random city from a supplied pool.</summary>
public class CityGenerator : AbstractGenerator<string>
{
    private readonly List<string> _cities;
    private readonly Random _random;

    public CityGenerator(List<string> cities, Random random)
    {
        _cities = cities;
        _random = random;
    }

    public override string Random() => _cities[_random.Next(_cities.Count)];

    public override long PoolSize() => _cities.Count;
}
