/*
 * Copyright 2024 Philterd, LLC
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

namespace Phileas.Model.Filtering;

public enum FilterType
{
    Age,
    BankRoutingNumber,
    BitcoinAddress,
    Currency,
    CreditCard,
    DriversLicenseNumber,
    LocationCity,
    LocationState,
    LocationCounty,
    Date,
    EmailAddress,
    FirstName,
    Hospital,
    HospitalAbbreviation,
    IbanCode,
    Identifier,
    IpAddress,
    MacAddress,
    PassportNumber,
    PhEye,
    PhoneNumber,
    PhoneNumberExtension,
    PhysicianName,
    Section,
    Ssn,
    StateAbbreviation,
    StreetAddress,
    Surname,
    TrackingNumber,
    Url,
    Vin,
    ZipCode,
    CustomDictionary,
    Person,
    MedicalCondition,
    Other
}

public static class FilterTypeExtensions
{
    public static string GetFilterTypeName(this FilterType filterType) => filterType switch
    {
        FilterType.Age => "age",
        FilterType.BankRoutingNumber => "bank-routing-number",
        FilterType.BitcoinAddress => "bitcoin-address",
        FilterType.Currency => "currency",
        FilterType.CreditCard => "credit-card",
        FilterType.DriversLicenseNumber => "drivers-license-number",
        FilterType.LocationCity => "location-city",
        FilterType.LocationState => "location-state",
        FilterType.LocationCounty => "location-county",
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
        FilterType.PhysicianName => "physician-name",
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
        FilterType.Person => "person",
        FilterType.MedicalCondition => "medical-condition",
        FilterType.Other => "other",
        _ => "unknown"
    };
}
