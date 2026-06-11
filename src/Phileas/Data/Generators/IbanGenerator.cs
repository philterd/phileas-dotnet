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

/// <summary>Generates random IBAN-style account numbers (US prefix), 24 characters long.</summary>
public class IbanGenerator : IGenerator<string>
{
    private readonly Random _random;

    public IbanGenerator(Random random) => _random = random;

    public string Random()
    {
        const string country = "US";
        var checkDigits = _random.Next(100).ToString("D2");
        // Java casts (long)(nextDouble()*1e20), which saturates to long.MaxValue when out of range.
        var raw = _random.NextDouble() * 1e20;
        var bban = raw >= long.MaxValue ? long.MaxValue : (long)raw;
        return country + checkDigits + bban.ToString("D20");
    }

    public long PoolSize() => long.MaxValue;
}
