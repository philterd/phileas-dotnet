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

/// <summary>Generates random email addresses of the form <c>first.surnameNNN@domain</c>.</summary>
public class EmailAddressGenerator : AbstractGenerator<string>
{
    private static readonly string[] DefaultDomains =
        { "gmail.com", "yahoo.com", "hotmail.com", "outlook.com", "example.com" };

    private readonly IGenerator<string> _firstNames;
    private readonly IGenerator<string> _surnames;
    private readonly Random _random;
    private readonly string[] _domains;

    public EmailAddressGenerator(Random random)
    {
        _random = random;
        _firstNames = new FirstNameGenerator(LoadNames("/first-names.txt"), random);
        _surnames = new SurnameGenerator(LoadNames("/surnames.txt"), random);
        _domains = DefaultDomains;
    }

    public EmailAddressGenerator(IGenerator<string> firstNames, IGenerator<string> surnames, Random random)
        : this(firstNames, surnames, random, DefaultDomains) { }

    public EmailAddressGenerator(IGenerator<string> firstNames, IGenerator<string> surnames, Random random,
        string[] domains)
    {
        _firstNames = firstNames;
        _surnames = surnames;
        _random = random;
        _domains = domains;
    }

    public override string Random()
    {
        var firstName = _firstNames.Random().ToLowerInvariant();
        var surname = _surnames.Random().ToLowerInvariant();
        var digits = _random.Next(1000).ToString("D3");
        var domain = _domains[_random.Next(_domains.Length)];
        return firstName + "." + surname + digits + "@" + domain;
    }

    public override long PoolSize() => _firstNames.PoolSize() * _surnames.PoolSize() * 1000L * _domains.Length;
}
