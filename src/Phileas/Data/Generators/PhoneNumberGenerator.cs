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

/// <summary>Generates random US phone numbers in <c>(XXX) XXX-XXXX</c> format.</summary>
public class PhoneNumberGenerator : IGenerator<string>
{
    private readonly Random _random;

    public PhoneNumberGenerator(Random random) => _random = random;

    public string Random()
    {
        var areaCode = _random.Next(900) + 100;
        var exchange = _random.Next(900) + 100;
        var subscriber = _random.Next(10000);
        return $"({areaCode:D3}) {exchange:D3}-{subscriber:D4}";
    }

    public long PoolSize() => 900L * 900L * 10000L;
}
