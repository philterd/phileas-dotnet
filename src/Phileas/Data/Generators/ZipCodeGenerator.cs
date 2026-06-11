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

/// <summary>Generates random five-digit ZIP codes, optionally constrained to real ZIP codes.</summary>
public class ZipCodeGenerator : AbstractGenerator<string>
{
    private readonly Random _random;
    private readonly bool _onlyValid;
    private readonly List<string> _validZipCodes = new();

    public ZipCodeGenerator(Random random) : this(random, false) { }

    public ZipCodeGenerator(Random random, bool onlyValid)
    {
        _random = random;
        _onlyValid = onlyValid;
        if (onlyValid)
        {
            foreach (var line in LoadNames("/zip-code-population.csv"))
            {
                if (!line.StartsWith('#'))
                {
                    _validZipCodes.Add(line.Split(',')[0]);
                }
            }
        }
    }

    public override string Random() =>
        _onlyValid ? _validZipCodes[_random.Next(_validZipCodes.Count)] : _random.Next(100000).ToString("D5");

    public override long PoolSize() => _onlyValid ? _validZipCodes.Count : 100000L;
}
