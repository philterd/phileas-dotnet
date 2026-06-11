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

using Phileas.Model;

namespace Phileas.Services.Anonymization;

/// <summary>
///     Maps a <see cref="FilterType" /> to the anonymization service that produces realistic fake values
///     for that entity type. Mirrors the Java <c>Filter.getAnonymizationService</c>: any type without a
///     dedicated service falls back to <see cref="AlphanumericAnonymizationService" />.
/// </summary>
public static class AnonymizationServiceFactory
{
    /// <summary>Creates the anonymization service for <paramref name="filterType" />.</summary>
    /// <param name="filterType">The entity type being anonymized.</param>
    /// <param name="contextService">The context service for referential-integrity replacements.</param>
    /// <param name="random">The random source.</param>
    /// <param name="candidates">Optional candidate values for the FROM_LIST method.</param>
    /// <param name="method">The anonymization method (defaults to REALISTIC).</param>
    /// <returns>The matching <see cref="IAnonymizationService" />.</returns>
    public static IAnonymizationService Create(FilterType filterType, IContextService contextService, Random random,
        List<string>? candidates = null, AnonymizationMethod method = AnonymizationMethod.Realistic)
    {
        candidates ??= new List<string>();

        // FROM_LIST when candidates are supplied; otherwise the requested method.
        IAnonymizationService WithMethod(Func<IContextService, Random, AnonymizationMethod, IAnonymizationService> realistic,
            Func<IContextService, Random, List<string>, IAnonymizationService> fromList)
            => candidates.Count > 0 ? fromList(contextService, random, candidates) : realistic(contextService, random, method);

        return filterType switch
        {
            FilterType.Age => WithMethod(
                (c, r, m) => new AgeAnonymizationService(c, r, m), (c, r, l) => new AgeAnonymizationService(c, r, l)),
            FilterType.BitcoinAddress => WithMethod(
                (c, r, m) => new BitcoinAddressAnonymizationService(c, r, m),
                (c, r, l) => new BitcoinAddressAnonymizationService(c, r, l)),
            FilterType.LocationCity => WithMethod(
                (c, r, m) => new CityAnonymizationService(c, r, m), (c, r, l) => new CityAnonymizationService(c, r, l)),
            FilterType.LocationCounty => WithMethod(
                (c, r, m) => new CountyAnonymizationService(c, r, m),
                (c, r, l) => new CountyAnonymizationService(c, r, l)),
            FilterType.CreditCard => WithMethod(
                (c, r, m) => new CreditCardAnonymizationService(c, r, m),
                (c, r, l) => new CreditCardAnonymizationService(c, r, l)),
            FilterType.Currency => WithMethod(
                (c, r, m) => new CurrencyAnonymizationService(c, r, m),
                (c, r, l) => new CurrencyAnonymizationService(c, r, l)),
            FilterType.Date => WithMethod(
                (c, r, m) => new DateAnonymizationService(c, r, m), (c, r, l) => new DateAnonymizationService(c, r, l)),
            FilterType.EmailAddress => WithMethod(
                (c, r, m) => new EmailAddressAnonymizationService(c, r, m),
                (c, r, l) => new EmailAddressAnonymizationService(c, r, l)),
            FilterType.Hospital => WithMethod(
                (c, r, m) => new HospitalAnonymizationService(c, r, m),
                (c, r, l) => new HospitalAnonymizationService(c, r, l)),
            FilterType.HospitalAbbreviation => WithMethod(
                (c, r, m) => new HospitalAbbreviationAnonymizationService(c, r, m),
                (c, r, l) => new HospitalAbbreviationAnonymizationService(c, r, l)),
            FilterType.IbanCode => WithMethod(
                (c, r, m) => new IbanCodeAnonymizationService(c, r, m),
                (c, r, l) => new IbanCodeAnonymizationService(c, r, l)),
            FilterType.IpAddress => WithMethod(
                (c, r, m) => new IpAddressAnonymizationService(c, r, m),
                (c, r, l) => new IpAddressAnonymizationService(c, r, l)),
            FilterType.MacAddress => WithMethod(
                (c, r, m) => new MacAddressAnonymizationService(c, r, m),
                (c, r, l) => new MacAddressAnonymizationService(c, r, l)),
            FilterType.PassportNumber => WithMethod(
                (c, r, m) => new PassportNumberAnonymizationService(c, r, m),
                (c, r, l) => new PassportNumberAnonymizationService(c, r, l)),
            FilterType.Person => WithMethod(
                (c, r, m) => new PersonsAnonymizationService(c, r, m),
                (c, r, l) => new PersonsAnonymizationService(c, r, l)),
            FilterType.LocationState => WithMethod(
                (c, r, m) => new StateAnonymizationService(c, r, m),
                (c, r, l) => new StateAnonymizationService(c, r, l)),
            FilterType.StateAbbreviation => WithMethod(
                (c, r, m) => new StateAbbreviationAnonymizationService(c, r, m),
                (c, r, l) => new StateAbbreviationAnonymizationService(c, r, l)),
            FilterType.StreetAddress => WithMethod(
                (c, r, m) => new StreetAddressAnonymizationService(c, r, m),
                (c, r, l) => new StreetAddressAnonymizationService(c, r, l)),
            FilterType.Surname => WithMethod(
                (c, r, m) => new SurnameAnonymizationService(c, r, m),
                (c, r, l) => new SurnameAnonymizationService(c, r, l)),
            FilterType.Url => WithMethod(
                (c, r, m) => new UrlAnonymizationService(c, r, m), (c, r, l) => new UrlAnonymizationService(c, r, l)),
            FilterType.ZipCode => WithMethod(
                (c, r, m) => new ZipCodeAnonymizationService(c, r, m),
                (c, r, l) => new ZipCodeAnonymizationService(c, r, l)),
            _ => WithMethod(
                (c, r, m) => new AlphanumericAnonymizationService(c, r, m),
                (c, r, l) => new AlphanumericAnonymizationService(c, r, l))
        };
    }
}
