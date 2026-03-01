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
using Phileas.Model.Filtering;
using Phileas.Policy.Filters;

namespace Phileas.Policy;

public class Identifiers
{
    [JsonPropertyName("pheyes")]
    public List<PhEye>? PhEyes { get; set; }

    [JsonPropertyName("age")]
    public Age? Age { get; set; }

    [JsonPropertyName("bankRoutingNumber")]
    public BankRoutingNumber? BankRoutingNumber { get; set; }

    [JsonPropertyName("bitcoinAddress")]
    public BitcoinAddress? BitcoinAddress { get; set; }

    [JsonPropertyName("creditCard")]
    public CreditCard? CreditCard { get; set; }

    [JsonPropertyName("currency")]
    public Currency? Currency { get; set; }

    [JsonPropertyName("date")]
    public Date? Date { get; set; }

    [JsonPropertyName("driversLicense")]
    public DriversLicense? DriversLicense { get; set; }

    [JsonPropertyName("emailAddress")]
    public EmailAddress? EmailAddress { get; set; }

    [JsonPropertyName("ibanCode")]
    public IbanCode? IbanCode { get; set; }

    [JsonPropertyName("ipAddress")]
    public IpAddress? IpAddress { get; set; }

    [JsonPropertyName("macAddress")]
    public MacAddress? MacAddress { get; set; }

    [JsonPropertyName("passportNumber")]
    public PassportNumber? PassportNumber { get; set; }

    [JsonPropertyName("phoneNumber")]
    public PhoneNumber? PhoneNumber { get; set; }

    [JsonPropertyName("phoneNumberExtension")]
    public PhoneNumberExtension? PhoneNumberExtension { get; set; }

    [JsonPropertyName("ssn")]
    public Ssn? Ssn { get; set; }

    [JsonPropertyName("stateAbbreviation")]
    public StateAbbreviation? StateAbbreviation { get; set; }

    [JsonPropertyName("streetAddress")]
    public StreetAddress? StreetAddress { get; set; }

    [JsonPropertyName("trackingNumber")]
    public TrackingNumber? TrackingNumber { get; set; }

    [JsonPropertyName("url")]
    public Url? Url { get; set; }

    [JsonPropertyName("vin")]
    public Vin? Vin { get; set; }

    [JsonPropertyName("zipCode")]
    public ZipCode? ZipCode { get; set; }

    public bool HasFilter(FilterType filterType) => filterType switch
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
        FilterType.PhoneNumber => PhoneNumber != null,
        FilterType.PhoneNumberExtension => PhoneNumberExtension != null,
        FilterType.Ssn => Ssn != null,
        FilterType.StateAbbreviation => StateAbbreviation != null,
        FilterType.StreetAddress => StreetAddress != null,
        FilterType.TrackingNumber => TrackingNumber != null,
        FilterType.Url => Url != null,
        FilterType.Vin => Vin != null,
        FilterType.ZipCode => ZipCode != null,
        _ => false
    };
}
