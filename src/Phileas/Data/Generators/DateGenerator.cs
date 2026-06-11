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

using System.Globalization;

namespace Phileas.Data.Generators;

/// <summary>Generates random dates within a year range, formatted with a pattern.</summary>
public class DateGenerator : IGenerator<string>
{
    private readonly Random _random;
    private readonly DateOnly _startDate;
    private readonly long _days;
    private readonly string _pattern;

    public DateGenerator(Random random) : this(random, 1970, 2030) { }

    public DateGenerator(Random random, int minYear, int maxYear) : this(random, minYear, maxYear, "yyyy-MM-dd") { }

    public DateGenerator(Random random, int minYear, int maxYear, string pattern)
    {
        _random = random;
        _startDate = new DateOnly(minYear, 1, 1);
        var endDate = new DateOnly(maxYear, 1, 1);
        _days = endDate.DayNumber - _startDate.DayNumber;
        _pattern = pattern;
    }

    public string Random() =>
        _startDate.AddDays(_random.Next((int)_days)).ToString(_pattern, CultureInfo.InvariantCulture);

    public long PoolSize() => _days;
}
