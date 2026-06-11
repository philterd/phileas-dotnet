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

/// <summary>Generates random passport numbers: one letter followed by eight digits.</summary>
public class PassportNumberGenerator : IGenerator<string>
{
    private readonly Random _random;

    public PassportNumberGenerator(Random random) => _random = random;

    public string Random() =>
        $"{(char)('A' + _random.Next(26))}{_random.Next(100000000):D8}";

    public long PoolSize() => 26L * 100000000L;
}
