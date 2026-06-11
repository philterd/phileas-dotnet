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

/// <summary>Generates random MAC addresses in colon-separated uppercase hex.</summary>
public class MacAddressGenerator : IGenerator<string>
{
    private readonly Random _random;

    public MacAddressGenerator(Random random) => _random = random;

    public string Random()
    {
        var mac = new byte[6];
        _random.NextBytes(mac);
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < mac.Length; i++)
        {
            sb.Append(mac[i].ToString("X2"));
            if (i < mac.Length - 1) sb.Append(':');
        }
        return sb.ToString();
    }

    public long PoolSize() => 256L * 256L * 256L * 256L * 256L * 256L;
}
