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
///     Rules-based filters report the canonical confidence for each pattern: the Java port's, except for its
///     two date patterns that report 75 and its URL pattern order under <c>requireHttpWwwPrefix: false</c>.
///     See philterd/phileas-dotnet#159 and philterd/phileas-conformance#7.
/// </summary>
public class RulesConfidenceTests
{
    private static string One(string filter, string settings = "") =>
        "{\"identifiers\":{\"" + filter + "\":{" + settings + "}}}";

    [Theory]
    [InlineData("bankRoutingNumber", "", "routing 111000025 on file", "111000025", 0.95)]
    [InlineData("currency", "", "total $1,234.56 due", "$1,234.56", 0.80)]
    [InlineData("currency", "", "total 1,234.56 USD due", "1,234.56 USD", 0.80)]
    [InlineData("date", "", "seen 2026-03-15 today", "2026-03-15", 0.75)]
    [InlineData("date", "", "seen 03-15-2026 today", "03-15-2026", 0.75)]
    [InlineData("date", "", "seen 3/15/26 today", "3/15/26", 0.75)] // Java reports 75 here
    [InlineData("date", "", "seen March 15, 2026 today", "March 15, 2026", 0.75)]
    [InlineData("driversLicense", "", "license A1234567 issued", "A1234567", 0.50)]
    [InlineData("driversLicense", "", "license 123456789 issued", "123456789", 0.50)]
    [InlineData("ein", "", "ein 12-3456789 on file", "12-3456789", 0.95)]
    [InlineData("emailAddress", "", "mail jane@example.com today", "jane@example.com", 0.90)]
    [InlineData("ipAddress", "", "host 192.0.2.10 is up", "192.0.2.10", 0.90)]
    [InlineData("ipAddress", "", "host 2001:db8::1 is up", "2001:db8::1", 0.90)]
    [InlineData("ipAddress", "", "build v1.2.3.4 released", "1.2.3.4", 0.90)]
    [InlineData("macAddress", "", "mac 00:1A:2B:3C:4D:5E seen", "00:1A:2B:3C:4D:5E", 0.90)]
    [InlineData("phoneNumberExtension", "", "call 555-0199 x1234 today", "x1234", 0.75)]
    [InlineData("stateAbbreviation", "", "moved to MD last year", "MD", 0.25)]
    [InlineData("url", "", "see https://www.example.com/path today", "https://www.example.com/path", 0.80)]
    [InlineData("url", "", "see www.example.com/path today", "www.example.com/path", 0.80)]
    [InlineData("url", "\"requireHttpWwwPrefix\":false", "see example.com today", "example.com", 0.10)]
    [InlineData("url", "\"requireHttpWwwPrefix\":false", "see https://www.example.com/path today", "https://www.example.com/path", 0.80)] // Java reports 0.1 here
    [InlineData("vin", "", "vin 1HGCM82633A004352 on file", "1HGCM82633A004352", 0.90)]
    public void EachPatternReportsTheCanonicalConfidence(string filter, string settings, string input, string text,
        double confidence)
    {
        var result = new FilterService().Filter(PolicySerializer.DeserializeFromJson(One(filter, settings)), "ctx", 0, input);

        var span = Assert.Single(result.Spans);
        Assert.Equal(text, span.Text);
        Assert.Equal(confidence, span.Confidence, 3);
    }

    // The Java port's credit card confidence modifiers, applied from the characters either side of the number.
    [Theory]
    [InlineData("", "card 4111111111111111 on file", 0.90)]
    [InlineData("", "ref-4111111111111111 on file", 0.60)]          // hyphen before
    [InlineData("", "card 4111111111111111-x on file", 0.60)]       // hyphen after
    [InlineData("", "ref-4111111111111111-x on file", 0.50)]        // hyphen on both sides
    [InlineData("\"onlyWordBoundaries\":false", "card 4111111111111111 on file", 0.90)]    // 0.7, +0.2 between spaces
    [InlineData("\"onlyWordBoundaries\":false", "ref-4111111111111111-x on file", 0.70)]   // 0.5, +0.2 between hyphens
    [InlineData("\"onlyWordBoundaries\":false", "ref-4111111111111111 on file", 0.80)]     // 0.6 for the hyphen, +0.2 for hyphen and space
    [InlineData("\"onlyWordBoundaries\":false", "refX4111111111111111X on file", 0.70)]    // letters either side
    public void CreditCardConfidenceFollowsTheCharactersAroundTheNumber(string settings, string input, double confidence)
    {
        var result = new FilterService().Filter(PolicySerializer.DeserializeFromJson(One("creditCard", settings)), "ctx", 0, input);

        var span = Assert.Single(result.Spans);
        Assert.Equal("4111111111111111", span.Text);
        Assert.Equal(confidence, span.Confidence, 3);
    }
}
