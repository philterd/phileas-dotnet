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

using Phileas.Policy;
using Phileas.Services;
using Xunit;

namespace Phileas.Tests;

/// <summary>
///     <c>%l</c> and <c>%v</c> in a <c>redactionFormat</c> behave as in the Java port: <c>%l</c> is the
///     classification or empty, and <c>%v</c> is the original text, inserted literally.
///     See philterd/phileas-dotnet#187.
/// </summary>
public class RedactionFormatPlaceholderTests
{
    private const string Format = "[%t|%l|%v]";

    private static string Strategies(string format) =>
        "[{\"strategy\":\"REDACT\",\"redactionFormat\":\"" + format + "\"}]";

    private static string Filter(string identifiersJson, string input) =>
        new FilterService().Filter(PolicySerializer.DeserializeFromJson("{\"identifiers\":{" + identifiersJson + "}}"),
            "ctx", 0, input).FilteredText;

    private static string CustomIdentifier(string pattern, string? classification) =>
        "\"identifiers\":[{\"pattern\":\"" + pattern + "\"," +
        (classification is null ? "" : "\"classification\":\"" + classification + "\",") +
        "\"identifierFilterStrategies\":" + Strategies(Format) + "}]";

    [Fact]
    public void FilterWithoutClassification_ReplacesLabelWithEmptyString()
    {
        Assert.Equal("SSN [ssn||123-45-6789] ok",
            Filter("\"ssn\":{\"ssnFilterStrategies\":" + Strategies(Format) + "}", "SSN 123-45-6789 ok"));
    }

    [Fact]
    public void TrackingNumber_KeepsCarrierLabel()
    {
        Assert.Equal("Ship [tracking-number|ups|1Z999AA10123456784] now",
            Filter("\"trackingNumber\":{\"trackingNumberFilterStrategies\":" + Strategies(Format) + "}",
                "Ship 1Z999AA10123456784 now"));
    }

    [Fact]
    public void CustomIdentifier_KeepsItsClassification()
    {
        Assert.Equal("Record [identifier|mrn|MRN 00482913] ok",
            Filter(CustomIdentifier("MRN \\\\d{8}", "mrn"), "Record MRN 00482913 ok"));
    }

    [Fact]
    public void PassportNumber_IsLabelledUs()
    {
        Assert.Equal("Passport [passport-number|US|C12345678] ok",
            Filter("\"passportNumber\":{\"passportNumberFilterStrategies\":" + Strategies(Format) + "}",
                "Passport C12345678 ok"));
    }

    [Fact]
    public void DriversLicense_HasEmptyLabel()
    {
        Assert.Equal("License [drivers-license-number||A1234567] ok",
            Filter("\"driversLicense\":{\"driversLicenseFilterStrategies\":" + Strategies(Format) + "}",
                "License A1234567 ok"));
    }

    [Fact]
    public void Value_IsInsertedLiterally()
    {
        // $ and \ are not substitution syntax, and a %l or %t in the text is not expanded.
        Assert.Equal("Id [identifier|custom-identifier|ID:a$1\\b%l%t] ok",
            Filter(CustomIdentifier("ID:\\\\S+", null), "Id ID:a$1\\b%l%t ok"));
    }

    [Fact]
    public void DefaultFormat_IsUnchanged()
    {
        Assert.Equal("SSN {{{REDACTED-ssn}}} ok",
            Filter("\"ssn\":{\"ssnFilterStrategies\":[{\"strategy\":\"REDACT\"}]}", "SSN 123-45-6789 ok"));
    }
}
