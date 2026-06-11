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

/// <summary>Generates random shipment tracking numbers (FedEx- or UPS-style).</summary>
public class TrackingNumberGenerator : IGenerator<string>
{
    private const string UpsChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private readonly Random _random;

    public TrackingNumberGenerator(Random random) => _random = random;

    public string Random() => _random.Next(2) == 0 ? GenerateFedEx() : GenerateUps();

    private string GenerateFedEx()
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < 12; i++) sb.Append(_random.Next(10));
        return sb.ToString();
    }

    private string GenerateUps()
    {
        var sb = new System.Text.StringBuilder("1Z");
        for (var i = 0; i < 16; i++) sb.Append(UpsChars[_random.Next(UpsChars.Length)]);
        return sb.ToString();
    }

    public long PoolSize() => long.MaxValue;
}
