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

/// <summary>Generates random URLs using first names as the domain stem.</summary>
public class UrlGenerator : IGenerator<string>
{
    private static readonly string[] DefaultProtocols = { "http", "https" };
    private static readonly string[] DefaultExtensions = { "com", "org", "net", "io", "gov" };

    private readonly IGenerator<string> _firstNames;
    private readonly Random _random;
    private readonly string[] _protocols;
    private readonly string[] _extensions;

    public UrlGenerator(IGenerator<string> firstNames, Random random)
        : this(firstNames, random, DefaultProtocols, DefaultExtensions) { }

    public UrlGenerator(IGenerator<string> firstNames, Random random, string[] protocols, string[] extensions)
    {
        _firstNames = firstNames;
        _random = random;
        _protocols = protocols;
        _extensions = extensions;
    }

    public string Random()
    {
        var domain = _firstNames.Random().ToLowerInvariant() + _random.Next(100);
        return _protocols[_random.Next(_protocols.Length)] + "://www." + domain + "."
               + _extensions[_random.Next(_extensions.Length)];
    }

    public long PoolSize() => (long)_protocols.Length * _firstNames.PoolSize() * 100L * _extensions.Length;
}
