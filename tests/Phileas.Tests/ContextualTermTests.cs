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

using Phileas.Filters.Rules;
using Phileas.Model;
using Phileas.Policy;
using Phileas.Services;
using Xunit;

namespace Phileas.Tests;

/// <summary>
///     A contextual term within <c>windowSize</c> words of a rules-based match raises its confidence by
///     <see cref="RulesFilter.ContextualTermBoost" />, capped at 1.0.
/// </summary>
public class ContextualTermTests
{
    private static Span One(string identifiersJson, string input)
    {
        var policy = PolicySerializer.DeserializeFromJson("{\"identifiers\": {" + identifiersJson + "}}");
        return Assert.Single(new FilterService().Filter(policy, "ctx", 0, input).Spans);
    }

    private static double Ssn(string input) => One("\"ssn\": {}", input).Confidence;

    [Fact]
    public void BoostIsFivePoints()
    {
        Assert.Equal(0.05, RulesFilter.ContextualTermBoost);
    }

    [Theory]
    [InlineData("SSN 123-45-6789 on file")] // before
    [InlineData("number 123-45-6789 is my ssn")] // after
    [InlineData("ssn 123-45-6789")] // lowercase
    [InlineData("SsN 123-45-6789")] // mixed case
    [InlineData("SSN: 123-45-6789")] // punctuation trimmed from the word
    [InlineData("(SSN) 123-45-6789")]
    [InlineData("SSN:123-45-6789")] // butted against the match
    [InlineData("SSN:\n123-45-6789")] // on the line before
    [InlineData("a b c d SSN 123-45-6789")] // the fifth word before
    [InlineData("123-45-6789 a b c d SSN")] // the fifth word after
    public void ATermInTheWindowRaisesConfidence(string input)
    {
        Assert.Equal(0.95, Ssn(input), 6);
    }

    [Theory]
    [InlineData("number 123-45-6789 on file")] // no term
    [InlineData("SSN a b c d e 123-45-6789")] // the sixth word before
    [InlineData("123-45-6789 a b c d e SSN")] // the sixth word after
    [InlineData("SSNs 123-45-6789")] // a longer word
    [InlineData("mySSN 123-45-6789")]
    [InlineData("social-security 123-45-6789")] // a hyphen inside a word is kept
    public void NoTermInTheWindowLeavesConfidence(string input)
    {
        Assert.Equal(0.90, Ssn(input), 6);
    }

    [Fact]
    public void AWordLongerThan64CharactersEndsTheSearch()
    {
        var word64 = new string('a', 64);
        var word65 = new string('a', 65);

        Assert.Equal(0.95, Ssn("SSN " + word64 + " 123-45-6789"), 6);
        Assert.Equal(0.90, Ssn("SSN " + word65 + " 123-45-6789"), 6);
        Assert.Equal(0.95, Ssn("123-45-6789 " + word64 + " SSN"), 6);
        Assert.Equal(0.90, Ssn("123-45-6789 " + word65 + " SSN"), 6);
    }

    [Fact]
    public void ATermStartingALongLineWithNoWhitespaceBoostsOnlyTheFirstMatch()
    {
        var result = new FilterService().Filter(
            PolicySerializer.DeserializeFromJson("{\"identifiers\": {\"ssn\": {}}}"), "ctx", 0,
            "ssn," + string.Join(",", Enumerable.Range(0, 10).Select(i => "123-45-678" + i)));

        Assert.Equal(10, result.Spans.Count);
        Assert.Equal(0.95, result.Spans[0].Confidence, 6); // "ssn," is the word before it
        Assert.All(result.Spans.Skip(1), span => Assert.Equal(0.90, span.Confidence, 6));
    }

    [Fact]
    public void SeveralTermsBoostOnce()
    {
        Assert.Equal(0.95, Ssn("SSN social security number 123-45-6789 ssn"), 6);
    }

    [Fact]
    public void BoostIsExactlyTheDecimalSum()
    {
        // 0.90 + 0.05 in binary floating point is 0.9500000000000001.
        Assert.Equal(0.95, Ssn("SSN 123-45-6789"));
    }

    [Theory]
    [InlineData("Phone: (555) 867-5309", 1.0)] // 0.95, capped
    [InlineData("Call (555) 867-5309", 0.95)]
    public void BoostIsCappedAtOne(string input, double expected)
    {
        Assert.Equal(expected, One("\"phoneNumber\": {}", input).Confidence, 6);
    }

    [Theory]
    [InlineData("Card: American Express 4111111111111111", 0.95)] // a term of two words
    [InlineData("American x Express 4111111111111111", 0.90)] // not in a row
    [InlineData("Express 4111111111111111 American", 0.90)] // split across the match
    public void ATermOfSeveralWordsMatchesThoseWordsInARow(string input, double expected)
    {
        Assert.Equal(expected, One("\"creditCard\": {\"onlyValidCreditCardNumbers\": false}", input).Confidence, 6);
    }

    [Fact]
    public void ATermWithPunctuationMatchesTheSameWord()
    {
        // The Java list has "d.o.b."; both it and the word are trimmed of punctuation at either end.
        Assert.Equal(0.80, One("\"date\": {}", "D.O.B.: 2026-03-15").Confidence, 6);
        Assert.Equal(0.75, One("\"date\": {}", "on: 2026-03-15").Confidence, 6);
    }

    [Fact]
    public void WindowSizeSetsHowFarTheTermsAreSought()
    {
        Assert.Equal(0.90, One("\"ssn\": {\"windowSize\": 1}", "SSN is 123-45-6789").Confidence, 6);
        Assert.Equal(0.95, One("\"ssn\": {\"windowSize\": 2}", "SSN is 123-45-6789").Confidence, 6);
        Assert.Equal(0.95, One("\"ssn\": {\"windowSize\": 10}", "SSN a b c d e f g h 123-45-6789").Confidence, 6);
    }

    [Fact]
    public void ABoostedConfidenceCanSatisfyAStrategyCondition()
    {
        const string json = "\"ssn\": {\"ssnFilterStrategies\": [" +
                            "{\"strategy\": \"LAST_4\", \"condition\": \"confidence > 0.92\"}, {\"strategy\": \"REDACT\"}]}";

        Assert.Equal("6789", One(json, "SSN 123-45-6789").Replacement);
        Assert.Equal("{{{REDACTED-ssn}}}", One(json, "number 123-45-6789").Replacement);
    }

    [Theory]
    [InlineData("SIN 271835464", FilterType.CanadaSin)]
    [InlineData("NAS 271835464", FilterType.CanadaSin)]
    [InlineData("social insurance number 271835464", FilterType.Ssn)] // "social" is an SSN term too: both boosted, a tie
    [InlineData("SSN 271835464", FilterType.Ssn)]
    [InlineData("number 271835464", FilterType.Ssn)] // a tie, decided by filter order
    public void ATermDecidesARunBothSsnAndSinMatch(string input, FilterType expected)
    {
        Assert.Equal(expected, One("\"canadaSin\": {}, \"ssn\": {}", input).FilterType);
    }

    // One row per identifier with contextual terms, so a filter whose analyzer lost its terms fails here.
    [Theory]
    [InlineData("age", "he is 42 years old today", "age: he is 42 years old")] // "years" inside the match does not count
    [InlineData("bankRoutingNumber", "ref 111000025 x", "routing 111000025 x")]
    [InlineData("bitcoinAddress", "ref 1BoatSLRHtKNngkdXEeobR76b53LETtpyT x", "wallet 1BoatSLRHtKNngkdXEeobR76b53LETtpyT x")]
    [InlineData("canadaSin", "ref 046 454 286 x", "SIN 046 454 286 x")]
    [InlineData("creditCard", "ref 4111111111111111 x", "card 4111111111111111 x")]
    [InlineData("currency", "total $1,234.56 x", "balance $1,234.56 x")]
    [InlineData("date", "seen 2026-03-15 x", "dob 2026-03-15 x")]
    [InlineData("driversLicense", "ref A1234567 x", "license A1234567 x")]
    [InlineData("ein", "ref 12-3456789 x", "EIN 12-3456789 x")]
    [InlineData("emailAddress", "mail jane@example.com x", "email jane@example.com x")]
    [InlineData("ibanCode", "ref GB82WEST12345698765432 x", "IBAN GB82WEST12345698765432 x")]
    [InlineData("ipAddress", "host 192.0.2.10 x", "ip 192.0.2.10 x")]
    [InlineData("macAddress", "seen 00:1A:2B:3C:4D:5E x", "mac 00:1A:2B:3C:4D:5E x")]
    [InlineData("passportNumber", "ref A12345678 x", "passport A12345678 x")]
    [InlineData("phoneNumber", "call +44 20 7946 0958 x", "phone +44 20 7946 0958 x")]
    [InlineData("phoneNumberExtension", "call 555-0199 x1234 now", "call 555-0199 x1234 extension")]
    [InlineData("ssn", "ref 123-45-6789 x", "SSN 123-45-6789 x")]
    [InlineData("stateAbbreviation", "moved to MD last year", "moved to the state of MD")]
    [InlineData("streetAddress", "at 123 Main Street now", "address: 123 Main Street now")]
    [InlineData("trackingNumber", "ref 1Z999AA10123456784 x", "tracking 1Z999AA10123456784 x")]
    [InlineData("url", "see https://www.example.com/path x", "website https://www.example.com/path x")]
    [InlineData("vin", "ref 1HGCM82633A004352 x", "VIN 1HGCM82633A004352 x")]
    [InlineData("zipCode", "in 90210 now", "zip 90210 now")]
    public void EveryIdentifierWithTermsIsBoosted(string filter, string plain, string withTerm)
    {
        var without = One("\"" + filter + "\": {}", plain).Confidence;
        var with = One("\"" + filter + "\": {}", withTerm).Confidence;
        Assert.Equal(Math.Min(1.0, Math.Round(without + RulesFilter.ContextualTermBoost, 6)), with, 6);
    }

    [Fact]
    public void ItinHasNoTerms()
    {
        // Neither philterd/phisql#59 nor the Java port names any.
        Assert.Equal(0.90, One("\"itin\": {}", "ITIN tax id 912-70-1234").Confidence, 6);
    }
}
