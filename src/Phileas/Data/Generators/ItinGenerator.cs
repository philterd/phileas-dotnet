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

/// <summary>
///     Generates random Individual Taxpayer Identification Numbers in <c>9XX-XX-XXXX</c> format, with the fourth and
///     fifth digits drawn from the ranges the IRS issues, so a generated value is detected with or without
///     <c>onlyValidRanges</c>.
/// </summary>
public class ItinGenerator : IGenerator<string>
{
    private static readonly int[] Groups = Enumerable.Range(50, 16)
        .Concat(Enumerable.Range(70, 19))
        .Concat(Enumerable.Range(90, 3))
        .Concat(Enumerable.Range(94, 6))
        .ToArray();

    private readonly Random _random;

    /// <summary>Creates a new <see cref="ItinGenerator" />.</summary>
    public ItinGenerator(Random random)
    {
        _random = random;
    }

    /// <inheritdoc />
    public string Random()
    {
        var area = 900 + _random.Next(100); // 900-999
        var group = Groups[_random.Next(Groups.Length)];
        var serial = _random.Next(10000); // 0000-9999
        return $"{area:D3}-{group:D2}-{serial:D4}";
    }

    /// <inheritdoc />
    public long PoolSize()
    {
        return 100L * Groups.Length * 10000L;
    }
}
