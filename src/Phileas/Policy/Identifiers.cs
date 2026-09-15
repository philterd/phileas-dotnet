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
    /// <summary>
    ///     Gets or sets the list of PhEye (NLP-based) filter configurations. Serialized as <c>pheyes</c>, matching the
    ///     canonical Phileas policy schema and the JSON produced by the PhiSQL <c>DETECT PHEYE</c> / <c>MODEL</c> clause;
    ///     a policy using the singular <c>pheye</c> key will not bind here.
    /// </summary>
    [JsonPropertyName("pheyes")]
    public List<PhEye>? PhEyes { get; set; }

    /// <summary>
    ///     Gets or sets the deprecated <c>dictionary</c> filter configurations.
    ///     <para>
    ///         The redaction policy schema declares only <c>dictionaries</c> and is
    ///         additionalProperties:false, so a policy carrying this .NET-only key did not validate.
    ///         Entries set here are folded into <see cref="CustomDictionaries" />, so an existing
    ///         policy keeps working, and the key is never written back: the getter returns
    ///         <see langword="null" /> and the serializer omits nulls.
    ///     </para>
    /// </summary>
    [JsonPropertyName("dictionary")]
    public List<Dictionary>? Dictionaries
    {
        get => null;
        set => _legacyDictionaries = value;
    }

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

    /// <summary>Gets or sets the Employer Identification Number (EIN) filter configuration.</summary>
    [JsonPropertyName("ein")]
    public Ein? Ein { get; set; }

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
    public List<CustomDictionary>? CustomDictionaries
    {
        get
        {
            // The stored list itself when there is no deprecated key to fold in, so nothing about the
            // ordinary path changes.
            if (_legacyDictionaries == null || _legacyDictionaries.Count == 0) return _customDictionaries;

            var merged = new List<CustomDictionary>(_customDictionaries ?? new List<CustomDictionary>());
            merged.AddRange(_legacyDictionaries.Select(dictionary => dictionary.ToCustomDictionary()));
            return merged;
        }
        set => _customDictionaries = value;
    }

    private List<CustomDictionary>? _customDictionaries;
    private List<Dictionary>? _legacyDictionaries;

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
        // A filter switched off with enabled:false is not one the policy has: FilterService does not
        // build it, and a filter constructed directly must produce nothing either. See #123.

        return filterType switch
        {
            FilterType.Age => Age is { Enabled: true },
            FilterType.BankRoutingNumber => BankRoutingNumber is { Enabled: true },
            FilterType.BitcoinAddress => BitcoinAddress is { Enabled: true },
            FilterType.CreditCard => CreditCard is { Enabled: true },
            FilterType.Currency => Currency is { Enabled: true },
            FilterType.Date => Date is { Enabled: true },
            FilterType.DriversLicenseNumber => DriversLicense is { Enabled: true },
            FilterType.Ein => Ein is { Enabled: true },
            FilterType.EmailAddress => EmailAddress is { Enabled: true },
            FilterType.IbanCode => IbanCode is { Enabled: true },
            FilterType.IpAddress => IpAddress is { Enabled: true },
            FilterType.MacAddress => MacAddress is { Enabled: true },
            FilterType.PassportNumber => PassportNumber is { Enabled: true },
            FilterType.PhEye => PhEyes?.Any(entry => entry.Enabled) == true,
            FilterType.PhoneNumber => PhoneNumber is { Enabled: true },
            FilterType.PhoneNumberExtension => PhoneNumberExtension is { Enabled: true },
            FilterType.Ssn => Ssn is { Enabled: true },
            FilterType.StateAbbreviation => StateAbbreviation is { Enabled: true },
            FilterType.StreetAddress => StreetAddress is { Enabled: true },
            FilterType.TrackingNumber => TrackingNumber is { Enabled: true },
            FilterType.Url => Url is { Enabled: true },
            FilterType.Vin => Vin is { Enabled: true },
            FilterType.ZipCode => ZipCode is { Enabled: true },
            FilterType.LocationCity => City is { Enabled: true },
            FilterType.LocationCounty => County is { Enabled: true },
            FilterType.LocationState => State is { Enabled: true },
            FilterType.Hospital => Hospital is { Enabled: true },
            FilterType.FirstName => FirstName is { Enabled: true },
            FilterType.Surname => Surname is { Enabled: true },
            FilterType.CustomDictionary => CustomDictionaries?.Any(entry => entry.Enabled) == true,
            FilterType.Identifier => CustomIdentifiers?.Any(entry => entry.Enabled) == true,
            FilterType.Section => Sections?.Any(entry => entry.Enabled) == true,
            _ => false
        };
    }
}