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

using System.Text.Json.Serialization;
using Phileas.Model;
using Phileas.Policy.Filters;

namespace Phileas.Policy;

/// <summary>
///     Contains the per-filter-type configuration objects that are active in a policy.
///     A filter is enabled when its corresponding property is non-<see langword="null" />
///     (or, for collection-based filters, when the collection is non-empty).
/// </summary>
public class Identifiers
{
    /// <summary>Gets or sets the list of PhEye (NLP-based) filter configurations.</summary>
    [JsonPropertyName("pheye")]
    public List<PhEye>? PhEyes { get; set; }

    /// <summary>Gets or sets the list of dictionary-based filter configurations.</summary>
    [JsonPropertyName("dictionary")]
    public List<Dictionary>? Dictionaries { get; set; }

    /// <summary>Gets or sets the age-expression filter configuration.</summary>
    [JsonPropertyName("age")]
    public Age? Age { get; set; }

    /// <summary>Gets or sets the bank routing number filter configuration.</summary>
    [JsonPropertyName("bankRoutingNumber")]
    public BankRoutingNumber? BankRoutingNumber { get; set; }

    /// <summary>Gets or sets the Bitcoin address filter configuration.</summary>
    [JsonPropertyName("bitcoinAddress")]
    public BitcoinAddress? BitcoinAddress { get; set; }

    /// <summary>Gets or sets the credit card number filter configuration.</summary>
    [JsonPropertyName("creditCard")]
    public CreditCard? CreditCard { get; set; }

    /// <summary>Gets or sets the currency amount filter configuration.</summary>
    [JsonPropertyName("currency")]
    public Currency? Currency { get; set; }

    /// <summary>Gets or sets the date expression filter configuration.</summary>
    [JsonPropertyName("date")]
    public Date? Date { get; set; }

    /// <summary>Gets or sets the driver's license number filter configuration.</summary>
    [JsonPropertyName("driversLicense")]
    public DriversLicense? DriversLicense { get; set; }

    /// <summary>Gets or sets the email address filter configuration.</summary>
    [JsonPropertyName("emailAddress")]
    public EmailAddress? EmailAddress { get; set; }

    /// <summary>Gets or sets the IBAN code filter configuration.</summary>
    [JsonPropertyName("ibanCode")]
    public IbanCode? IbanCode { get; set; }

    /// <summary>Gets or sets the IP address filter configuration.</summary>
    [JsonPropertyName("ipAddress")]
    public IpAddress? IpAddress { get; set; }

    /// <summary>Gets or sets the MAC address filter configuration.</summary>
    [JsonPropertyName("macAddress")]
    public MacAddress? MacAddress { get; set; }

    /// <summary>Gets or sets the passport number filter configuration.</summary>
    [JsonPropertyName("passportNumber")]
    public PassportNumber? PassportNumber { get; set; }

    /// <summary>Gets or sets the phone number filter configuration.</summary>
    [JsonPropertyName("phoneNumber")]
    public PhoneNumber? PhoneNumber { get; set; }

    /// <summary>Gets or sets the phone number extension filter configuration.</summary>
    [JsonPropertyName("phoneNumberExtension")]
    public PhoneNumberExtension? PhoneNumberExtension { get; set; }

    /// <summary>Gets or sets the Social Security Number (SSN) filter configuration.</summary>
    [JsonPropertyName("ssn")]
    public Ssn? Ssn { get; set; }

    /// <summary>Gets or sets the US state abbreviation filter configuration.</summary>
    [JsonPropertyName("stateAbbreviation")]
    public StateAbbreviation? StateAbbreviation { get; set; }

    /// <summary>Gets or sets the street address filter configuration.</summary>
    [JsonPropertyName("streetAddress")]
    public StreetAddress? StreetAddress { get; set; }

    /// <summary>Gets or sets the package tracking number filter configuration.</summary>
    [JsonPropertyName("trackingNumber")]
    public TrackingNumber? TrackingNumber { get; set; }

    /// <summary>Gets or sets the URL filter configuration.</summary>
    [JsonPropertyName("url")]
    public Url? Url { get; set; }

    /// <summary>Gets or sets the Vehicle Identification Number (VIN) filter configuration.</summary>
    [JsonPropertyName("vin")]
    public Vin? Vin { get; set; }

    /// <summary>Gets or sets the ZIP code filter configuration.</summary>
    [JsonPropertyName("zipCode")]
    public ZipCode? ZipCode { get; set; }

    /// <summary>Gets or sets the city (dictionary) filter configuration.</summary>
    [JsonPropertyName("city")]
    public City? City { get; set; }

    /// <summary>Gets or sets the county (dictionary) filter configuration.</summary>
    [JsonPropertyName("county")]
    public County? County { get; set; }

    /// <summary>Gets or sets the state (dictionary) filter configuration.</summary>
    [JsonPropertyName("state")]
    public State? State { get; set; }

    /// <summary>Gets or sets the hospital (dictionary) filter configuration.</summary>
    [JsonPropertyName("hospital")]
    public Hospital? Hospital { get; set; }

    /// <summary>Gets or sets the first-name (dictionary) filter configuration.</summary>
    [JsonPropertyName("firstName")]
    public FirstName? FirstName { get; set; }

    /// <summary>Gets or sets the surname (dictionary) filter configuration.</summary>
    [JsonPropertyName("surname")]
    public Surname? Surname { get; set; }

    /// <summary>Gets or sets the list of user-supplied custom dictionaries. This is the canonical
    ///     <c>"dictionaries"</c> identifier (matching the PhiSQL <c>DEFINE DICTIONARY</c> output).</summary>
    [JsonPropertyName("dictionaries")]
    public List<CustomDictionary>? CustomDictionaries { get; set; }

    /// <summary>Gets or sets the list of custom regex-based identifier filters.</summary>
    [JsonPropertyName("identifiers")]
    public List<Identifier>? CustomIdentifiers { get; set; }

    /// <summary>Gets or sets the list of section filters.</summary>
    [JsonPropertyName("sections")]
    public List<Section>? Sections { get; set; }

    /// <summary>
    ///     Determines whether the filter for the specified <paramref name="filterType" /> is enabled in this identifiers
    ///     configuration.
    /// </summary>
    /// <param name="filterType">The filter type to check.</param>
    /// <returns><see langword="true" /> if the filter is configured and enabled; otherwise <see langword="false" />.</returns>
    public bool HasFilter(FilterType filterType)
    {
        return filterType switch
        {
            FilterType.Age => Age != null,
            FilterType.BankRoutingNumber => BankRoutingNumber != null,
            FilterType.BitcoinAddress => BitcoinAddress != null,
            FilterType.CreditCard => CreditCard != null,
            FilterType.Currency => Currency != null,
            FilterType.Date => Date != null,
            FilterType.DriversLicenseNumber => DriversLicense != null,
            FilterType.EmailAddress => EmailAddress != null,
            FilterType.IbanCode => IbanCode != null,
            FilterType.IpAddress => IpAddress != null,
            FilterType.MacAddress => MacAddress != null,
            FilterType.PassportNumber => PassportNumber != null,
            FilterType.PhEye => PhEyes != null && PhEyes.Count > 0,
            FilterType.Dictionary => Dictionaries != null && Dictionaries.Count > 0,
            FilterType.PhoneNumber => PhoneNumber != null,
            FilterType.PhoneNumberExtension => PhoneNumberExtension != null,
            FilterType.Ssn => Ssn != null,
            FilterType.StateAbbreviation => StateAbbreviation != null,
            FilterType.StreetAddress => StreetAddress != null,
            FilterType.TrackingNumber => TrackingNumber != null,
            FilterType.Url => Url != null,
            FilterType.Vin => Vin != null,
            FilterType.ZipCode => ZipCode != null,
            FilterType.LocationCity => City != null,
            FilterType.LocationCounty => County != null,
            FilterType.LocationState => State != null,
            FilterType.Hospital => Hospital != null,
            FilterType.FirstName => FirstName != null,
            FilterType.Surname => Surname != null,
            FilterType.CustomDictionary => CustomDictionaries != null && CustomDictionaries.Count > 0,
            FilterType.Identifier => CustomIdentifiers != null && CustomIdentifiers.Count > 0,
            FilterType.Section => Sections != null && Sections.Count > 0,
            _ => false
        };
    }
}