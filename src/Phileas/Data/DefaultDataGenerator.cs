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

using Phileas.Data.Generators;

namespace Phileas.Data;

/// <summary>
///     Default <see cref="IDataGenerator" />: constructs every generator, loading dictionary pools from
///     the embedded resources. Mirrors the Java <c>DefaultDataGenerator</c>.
/// </summary>
public class DefaultDataGenerator : AbstractGenerator<object?>, IDataGenerator
{
    private readonly Random _random;

    private readonly IGenerator<string> _firstNames;
    private readonly IGenerator<string> _surnames;
    private readonly IGenerator<string> _fullNames;
    private readonly IGenerator<string> _ssn;
    private readonly IGenerator<string> _phoneNumbers;
    private readonly IGenerator<string> _emailAddresses;
    private readonly IGenerator<int> _age;
    private readonly IGenerator<string> _bankRoutingNumbers;
    private readonly IGenerator<string> _creditCardNumbers;
    private readonly IGenerator<string> _dates;
    private readonly IGenerator<string> _iban;
    private readonly IGenerator<string> _ipAddresses;
    private readonly IGenerator<string> _macAddresses;
    private readonly IGenerator<string> _passportNumbers;
    private readonly IGenerator<string> _states;
    private readonly IGenerator<string> _stateAbbreviations;
    private readonly IGenerator<string> _zipCodes;
    private readonly IGenerator<string> _bitcoinAddresses;
    private readonly IGenerator<string> _vin;
    private readonly IGenerator<string> _urls;
    private readonly IGenerator<string> _hospitals;
    private readonly IGenerator<string> _trackingNumbers;
    private readonly IGenerator<string> _cities;
    private readonly IGenerator<string> _streetAddresses;
    private readonly IGenerator<string> _counties;

    /// <summary>Creates a generator backed by a fresh <see cref="System.Random" />.</summary>
    public DefaultDataGenerator() : this(new Random()) { }

    /// <summary>Creates a generator backed by the supplied <paramref name="random" />.</summary>
    public DefaultDataGenerator(Random random)
    {
        _random = random;
        var firstNamesList = LoadNames("/first-names.txt");
        var surnamesList = LoadNames("/surnames.txt");
        _firstNames = new FirstNameGenerator(firstNamesList, random);
        _surnames = new SurnameGenerator(surnamesList, random);
        _fullNames = new FullNameGenerator(_firstNames, _surnames);
        _ssn = new SsnGenerator(random);
        _phoneNumbers = new PhoneNumberGenerator(random);
        _emailAddresses = new EmailAddressGenerator(_firstNames, _surnames, random);
        _age = new AgeGenerator(random);
        _bankRoutingNumbers = new BankRoutingNumberGenerator(random);
        _creditCardNumbers = new CreditCardNumberGenerator(random);
        _dates = new DateGenerator(random);
        _iban = new IbanGenerator(random);
        _ipAddresses = new IpAddressGenerator(random);
        _macAddresses = new MacAddressGenerator(random);
        _passportNumbers = new PassportNumberGenerator(random);
        _states = new StateGenerator(random);
        _stateAbbreviations = new StateAbbreviationGenerator(random);
        _zipCodes = new ZipCodeGenerator(random);
        _bitcoinAddresses = new BitcoinAddressGenerator(random);
        _vin = new VinGenerator(random);
        _urls = new UrlGenerator(_firstNames, random);
        _hospitals = new HospitalGenerator(random);
        _trackingNumbers = new TrackingNumberGenerator(random);
        _cities = new CityGenerator(LoadNames("/cities.txt"), random);
        _streetAddresses = new StreetAddressGenerator(_surnames, random);
        _counties = new CountyGenerator(LoadNames("/counties.txt"), random);
    }

    public IGenerator<string> FirstNames() => _firstNames;
    public IGenerator<string> Surnames() => _surnames;
    public IGenerator<string> FullNames() => _fullNames;
    public IGenerator<string> Ssn() => _ssn;
    public IGenerator<string> PhoneNumbers() => _phoneNumbers;
    public IGenerator<string> EmailAddresses() => _emailAddresses;
    public IGenerator<int> Age() => _age;
    public IGenerator<string> BankRoutingNumbers() => _bankRoutingNumbers;
    public IGenerator<string> CreditCardNumbers() => _creditCardNumbers;
    public IGenerator<string> Dates() => _dates;
    public IGenerator<string> Iban() => _iban;
    public IGenerator<string> IpAddresses() => _ipAddresses;
    public IGenerator<string> MacAddresses() => _macAddresses;
    public IGenerator<string> PassportNumbers() => _passportNumbers;
    public IGenerator<string> States() => _states;
    public IGenerator<string> StateAbbreviations() => _stateAbbreviations;
    public IGenerator<string> ZipCodes() => _zipCodes;
    public IGenerator<string> BitcoinAddresses() => _bitcoinAddresses;
    public IGenerator<string> Vin() => _vin;
    public IGenerator<string> Urls() => _urls;
    public IGenerator<string> Hospitals() => _hospitals;
    public IGenerator<string> TrackingNumbers() => _trackingNumbers;
    public IGenerator<string> StreetAddresses() => _streetAddresses;
    public IGenerator<string> Cities() => _cities;
    public IGenerator<string> Counties() => _counties;
    public IGenerator<string> CustomId(string pattern) => new CustomIdGenerator(_random, pattern);
    public IGenerator<string> Dates(string pattern) => new DateGenerator(_random, 1970, 2030, pattern);

    /// <inheritdoc />
    public override object? Random() => null;

    /// <inheritdoc />
    public override long PoolSize() => 0;
}
