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

/// <summary>Combines a first name and a surname into a full name.</summary>
public class FullNameGenerator : IGenerator<string>
{
    private readonly IGenerator<string> _firstNames;
    private readonly IGenerator<string> _surnames;

    public FullNameGenerator(IGenerator<string> firstNames, IGenerator<string> surnames)
    {
        _firstNames = firstNames;
        _surnames = surnames;
    }

    public string Random() => _firstNames.Random() + " " + _surnames.Random();

    public long PoolSize() => _firstNames.PoolSize() * _surnames.PoolSize();
}
