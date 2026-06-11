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

/// <summary>Generates random Social Security numbers in <c>XXX-XX-XXXX</c> format.</summary>
public class SsnGenerator : IGenerator<string>
{
    private readonly Random _random;

    /// <summary>Creates a new <see cref="SsnGenerator" />.</summary>
    public SsnGenerator(Random random)
    {
        _random = random;
    }

    /// <inheritdoc />
    public string Random()
    {
        var area = _random.Next(900) + 100; // 100-999
        var group = _random.Next(100); // 00-99
        var serial = _random.Next(10000); // 0000-9999
        return $"{area:D3}-{group:D2}-{serial:D4}";
    }

    /// <inheritdoc />
    public long PoolSize()
    {
        return 900L * 100L * 10000L;
    }
}
