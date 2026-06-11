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

/// <summary>Generates random street addresses using surnames as street names.</summary>
public class StreetAddressGenerator : AbstractGenerator<string>
{
    private static readonly string[] Suffixes =
        { "St", "Ave", "Blvd", "Rd", "Ln", "Dr", "Ct", "Pl", "Way", "Ter" };

    private readonly IGenerator<string> _surnames;
    private readonly Random _random;

    public StreetAddressGenerator(IGenerator<string> surnames, Random random)
    {
        _surnames = surnames;
        _random = random;
    }

    public override string Random()
    {
        var houseNumber = _random.Next(9999) + 1;
        var streetName = _surnames.Random();
        var suffix = Suffixes[_random.Next(Suffixes.Length)];
        return houseNumber + " " + streetName + " " + suffix;
    }

    public override long PoolSize() => 9999L * _surnames.PoolSize() * Suffixes.Length;
}
