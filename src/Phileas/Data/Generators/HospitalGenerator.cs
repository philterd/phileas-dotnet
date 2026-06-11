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

/// <summary>Selects a random hospital name, loading the pool from an embedded resource by default.</summary>
public class HospitalGenerator : AbstractGenerator<string>
{
    private readonly List<string> _hospitals;
    private readonly Random _random;

    public HospitalGenerator(Random random)
    {
        _random = random;
        _hospitals = LoadNames("/hospitals.txt");
    }

    public HospitalGenerator(List<string> hospitals, Random random)
    {
        _random = random;
        _hospitals = hospitals;
    }

    public override string Random() => _hospitals[_random.Next(_hospitals.Count)];

    public override long PoolSize() => _hospitals.Count;
}
