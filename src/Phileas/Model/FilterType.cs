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

namespace Phileas.Model;

/// <summary>
///     Identifies the category of a detected entity.
///     Each value maps to a human-readable slug via <see cref="FilterTypeExtensions.GetFilterTypeName" />.
/// </summary>
public enum FilterType
{
    /// <summary>Age expressions such as "32 years old".</summary>
    Age,

    /// <summary>US bank routing (ABA) numbers.</summary>
    BankRoutingNumber,

    /// <summary>Bitcoin wallet addresses.</summary>
    BitcoinAddress,

    /// <summary>Currency amounts.</summary>
    Currency,

    /// <summary>Credit card numbers.</summary>
    CreditCard,

    /// <summary>Driver's license numbers.</summary>
    DriversLicenseNumber,

    /// <summary>US Employer Identification Numbers (EIN, federal tax ID).</summary>
    Ein,

    /// <summary>City names.</summary>
    LocationCity,

    /// <summary>US state names.</summary>
    LocationState,

    /// <summary>County names.</summary>
    LocationCounty,

    /// <summary>Date expressions.</summary>
    Date,

    /// <summary>Email addresses.</summary>
    EmailAddress,

    /// <summary>Given (first) names.</summary>
    FirstName,

    /// <summary>Hospital names.</summary>
    Hospital,

    /// <summary>Hospital abbreviations.</summary>
    HospitalAbbreviation,

    /// <summary>International Bank Account Numbers (IBAN).</summary>
    IbanCode,

    /// <summary>Generic identifiers.</summary>
    Identifier,

    /// <summary>IPv4 and IPv6 addresses.</summary>
    IpAddress,

    /// <summary>MAC (hardware) addresses.</summary>
    MacAddress,

    /// <summary>Passport numbers.</summary>
    PassportNumber,

    /// <summary>Entities detected by the PhEye NLP service.</summary>
    PhEye,

    /// <summary>Phone numbers.</summary>
    PhoneNumber,

    /// <summary>Phone number extensions.</summary>
    PhoneNumberExtension,

    /// <summary>Document sections.</summary>
    Section,

    /// <summary>US Social Security Numbers (SSN).</summary>
    Ssn,

    /// <summary>US state abbreviations.</summary>
    StateAbbreviation,

    /// <summary>Street addresses.</summary>
    StreetAddress,

    /// <summary>Surnames (family names).</summary>
    Surname,

    /// <summary>Package tracking numbers.</summary>
    TrackingNumber,

    /// <summary>URLs and web addresses.</summary>
    Url,

    /// <summary>Vehicle Identification Numbers (VIN).</summary>
    Vin,

    /// <summary>US ZIP codes.</summary>
    ZipCode,

    /// <summary>Entities matched by a custom dictionary.</summary>
    CustomDictionary,

    /// <summary>Entities matched by a built-in dictionary.</summary>
    Dictionary,

    /// <summary>Person names detected by an NLP model.</summary>
    Person,

    /// <summary>Medical conditions.</summary>
    MedicalCondition,

    /// <summary>Entities that do not fit any of the above categories.</summary>
    Other
}

/// <summary>
///     Extension methods for <see cref="FilterType" />.
/// </summary>
public static class FilterTypeExtensions
{
    /// <summary>
    ///     Returns the kebab-case slug for the given <paramref name="filterType" />,
    ///     used in redaction format strings (e.g. <c>{{{REDACTED-email-address}}}</c>).
    /// </summary>
    /// <param name="filterType">The filter type to convert.</param>
    /// <returns>A lowercase, hyphen-separated string representation of the filter type.</returns>
    public static string GetFilterTypeName(this FilterType filterType)
    {
        return filterType switch
        {
            FilterType.Age => "age",
            FilterType.BankRoutingNumber => "bank-routing-number",
            FilterType.BitcoinAddress => "bitcoin-address",
            FilterType.Currency => "currency",
            FilterType.CreditCard => "credit-card",
            FilterType.DriversLicenseNumber => "drivers-license-number",
            FilterType.Ein => "ein",
            FilterType.LocationCity => "city",
            FilterType.LocationState => "state",
            FilterType.LocationCounty => "county",
            FilterType.Date => "date",
            FilterType.EmailAddress => "email-address",
            FilterType.FirstName => "first-name",
            FilterType.Hospital => "hospital",
            FilterType.HospitalAbbreviation => "hospital-abbreviation",
            FilterType.IbanCode => "iban-code",
            FilterType.Identifier => "identifier",
            FilterType.IpAddress => "ip-address",
            FilterType.MacAddress => "mac-address",
            FilterType.PassportNumber => "passport-number",
            FilterType.PhEye => "ph-eye",
            FilterType.PhoneNumber => "phone-number",
            FilterType.PhoneNumberExtension => "phone-number-extension",
            FilterType.Section => "section",
            FilterType.Ssn => "ssn",
            FilterType.StateAbbreviation => "state-abbreviation",
            FilterType.StreetAddress => "street-address",
            FilterType.Surname => "surname",
            FilterType.TrackingNumber => "tracking-number",
            FilterType.Url => "url",
            FilterType.Vin => "vin",
            FilterType.ZipCode => "zip-code",
            FilterType.CustomDictionary => "custom-dictionary",
            FilterType.Dictionary => "dictionary",
            FilterType.Person => "person",
            FilterType.MedicalCondition => "medical-condition",
            FilterType.Other => "other",
            _ => "unknown"
        };
    }
}