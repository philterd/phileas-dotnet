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
///     Factory exposing a generator for each supported data type. Mirrors the Java <c>DataGenerator</c>
///     interface.
/// </summary>
public interface IDataGenerator
{
    IGenerator<string> FirstNames();
    IGenerator<string> Surnames();
    IGenerator<string> FullNames();
    IGenerator<string> Ssn();
    IGenerator<string> PhoneNumbers();
    IGenerator<string> EmailAddresses();
    IGenerator<int> Age();
    IGenerator<string> BankRoutingNumbers();
    IGenerator<string> CreditCardNumbers();
    IGenerator<string> Dates();
    IGenerator<string> Iban();
    IGenerator<string> IpAddresses();
    IGenerator<string> MacAddresses();
    IGenerator<string> PassportNumbers();
    IGenerator<string> States();
    IGenerator<string> StateAbbreviations();
    IGenerator<string> ZipCodes();
    IGenerator<string> BitcoinAddresses();
    IGenerator<string> Vin();
    IGenerator<string> Urls();
    IGenerator<string> Hospitals();
    IGenerator<string> TrackingNumbers();
    IGenerator<string> StreetAddresses();
    IGenerator<string> Cities();
    IGenerator<string> Counties();
    IGenerator<string> CustomId(string pattern);
    IGenerator<string> Dates(string pattern);
}
