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

using System.Text.Json;
using Phileas.Filters.Rules.Regex.RegexFilters;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
///     Filter options bound from the policy. Each test asserts the option changes what is detected,
///     not merely that it deserializes. See philterd/phileas-dotnet#84.
/// </summary>
public class FilterOptionTests
{
    private static IList<Model.Span> Detect(Identifiers identifiers, string input)
    {
        return new FilterService().Filter(new PhileasPolicy { Name = "t", Identifiers = identifiers },
            "ctx", 0, input).Spans;
    }

    // ---------------- creditCard ----------------

    [Fact]
    public void OnlyValidCreditCardNumbers_DropsNumbersFailingLuhn()
    {
        const string input = "Card 4111111111111112 here"; // last digit altered, fails Luhn

        Assert.Empty(Detect(new Identifiers { CreditCard = new CreditCard() }, input));
        Assert.NotEmpty(Detect(
            new Identifiers { CreditCard = new CreditCard { OnlyValidCreditCardNumbers = false } }, input));
    }

    [Fact]
    public void OnlyWordBoundaries_DecidesWhetherAnEmbeddedNumberIsFound()
    {
        const string input = "ref=x4111111111111111y";

        Assert.Empty(Detect(new Identifiers { CreditCard = new CreditCard() }, input));
        Assert.NotEmpty(Detect(
            new Identifiers { CreditCard = new CreditCard { OnlyWordBoundaries = false } }, input));
    }

    [Theory]
    [InlineData("1500000000000")]
    [InlineData("1612345678901")]
    [InlineData("1799999999999")]
    public void IgnoreWhenInUnixTimestamp_DropsTimestampsTheShapePatternPicksUp(string timestamp)
    {
        // Detection is brand-agnostic, so an epoch millisecond value is a candidate whenever
        // validation is off. That is the case this option exists for. See #102.
        var input = "value " + timestamp + " here";
        var withoutGuard = new Identifiers
        {
            CreditCard = new CreditCard { OnlyValidCreditCardNumbers = false }
        };
        var withGuard = new Identifiers
        {
            CreditCard = new CreditCard
            {
                OnlyValidCreditCardNumbers = false, IgnoreWhenInUnixTimestamp = true
            }
        };

        Assert.NotEmpty(Detect(withoutGuard, input));
        Assert.Empty(Detect(withGuard, input));
    }

    [Fact]
    public void IgnoreWhenInUnixTimestamp_DoesNotDropRealCards()
    {
        // The guard must not cost recall: a card is not a thirteen-digit epoch value.
        var guarded = new Identifiers
        {
            CreditCard = new CreditCard { IgnoreWhenInUnixTimestamp = true }
        };

        Assert.NotEmpty(Detect(guarded, "card 4111111111111111 end"));
    }

    [Theory]
    [InlineData("1500000000000", true)] // 13 digits, epoch milliseconds
    [InlineData("1612345678901", true)]
    [InlineData("1899999999999", true)]
    [InlineData("1400000000000", false)] // prefix outside 15-18
    [InlineData("1900000000000", false)]
    [InlineData("150000000000", false)] // 12 digits
    [InlineData("15000000000000", false)] // 14 digits
    [InlineData("4111111111111111", false)] // a card number
    [InlineData("", false)]
    public void IsUnixTimestamp_RecognisesTheEpochMillisecondShape(string text, bool expected)
    {
        // Tested directly because the guard cannot currently change the filter's output, so a mistake
        // in the pattern would otherwise go unnoticed.
        Assert.Equal(expected, CreditCardFilter.IsUnixTimestamp(text));
    }

    // ---------------- emailAddress ----------------

    [Fact]
    public void OnlyStrictMatches_DecidesWhetherRfcSpecialsAreAccepted()
    {
        // The RFC permits these in an unquoted local part; the lenient pattern does not.
        const string input = "write to jo!smith@example.com now";

        var strict = Detect(new Identifiers { EmailAddress = new EmailAddress() }, input);
        var lenient = Detect(
            new Identifiers { EmailAddress = new EmailAddress { OnlyStrictMatches = false } }, input);

        Assert.Contains(strict, s => s.Text == "jo!smith@example.com");
        Assert.DoesNotContain(lenient, s => s.Text == "jo!smith@example.com");
    }

    [Fact]
    public void OnlyValidTLDs_DropsAddressesOnUnregisteredTopLevelDomains()
    {
        const string input = "mail alice@example.invalidtld here";

        Assert.NotEmpty(Detect(new Identifiers { EmailAddress = new EmailAddress() }, input));
        Assert.Empty(Detect(
            new Identifiers { EmailAddress = new EmailAddress { OnlyValidTLDs = true } }, input));
    }

    [Fact]
    public void OnlyValidTLDs_KeepsAddressesOnRegisteredOnes()
    {
        Assert.NotEmpty(Detect(new Identifiers { EmailAddress = new EmailAddress { OnlyValidTLDs = true } },
            "mail alice@example.com here"));
    }

    // ---------------- ibanCode ----------------

    [Fact]
    public void OnlyValidIBANCodes_DropsCodesFailingTheChecksum()
    {
        const string input = "IBAN GB82WEST12345698765433 here"; // last digit altered

        Assert.Empty(Detect(new Identifiers { IbanCode = new IbanCode() }, input));
        Assert.NotEmpty(Detect(
            new Identifiers { IbanCode = new IbanCode { OnlyValidIBANCodes = false } }, input));
    }

    [Fact]
    public void AllowSpaces_DecidesWhetherAGroupedIbanIsFound()
    {
        const string input = "IBAN GB82 WEST 1234 5698 7654 32 here";

        Assert.NotEmpty(Detect(new Identifiers { IbanCode = new IbanCode() }, input));
        Assert.Empty(Detect(new Identifiers { IbanCode = new IbanCode { AllowSpaces = false } }, input));
    }

    // ---------------- trackingNumber ----------------

    [Fact]
    public void Ups_DecidesWhetherAUpsNumberIsFound()
    {
        const string input = "Parcel 1Z999AA10123456784 here";

        Assert.NotEmpty(Detect(new Identifiers { TrackingNumber = new TrackingNumber() }, input));
        Assert.Empty(Detect(
            new Identifiers { TrackingNumber = new TrackingNumber { Ups = false } }, input));
    }

    [Fact]
    public void Usps_DecidesWhetherALongDigitRunIsFound()
    {
        const string input = "Parcel 94001234567890123456 here";

        Assert.NotEmpty(Detect(new Identifiers { TrackingNumber = new TrackingNumber() }, input));
        Assert.Empty(Detect(
            new Identifiers { TrackingNumber = new TrackingNumber { Usps = false, Fedex = false } }, input));
    }

    [Fact]
    public void Fedex_DecidesWhetherAMediumDigitRunIsFound()
    {
        const string input = "Parcel 123456789012 here";

        Assert.NotEmpty(Detect(new Identifiers { TrackingNumber = new TrackingNumber() }, input));
        Assert.Empty(Detect(
            new Identifiers { TrackingNumber = new TrackingNumber { Fedex = false } }, input));
    }

    [Fact]
    public void AllowSpaces_DecidesWhetherAGroupedTrackingNumberIsFound()
    {
        const string input = "Parcel 1234 5678 9012 here";

        Assert.Empty(Detect(new Identifiers { TrackingNumber = new TrackingNumber() }, input));
        Assert.NotEmpty(Detect(
            new Identifiers { TrackingNumber = new TrackingNumber { AllowSpaces = true } }, input));
    }

    // ---------------- url ----------------

    [Fact]
    public void RequireHttpWwwPrefix_DecidesWhetherABareHostIsFound()
    {
        const string input = "see example.com/page for more";

        Assert.Empty(Detect(new Identifiers { Url = new Url() }, input));
        Assert.NotEmpty(Detect(new Identifiers { Url = new Url { RequireHttpWwwPrefix = false } }, input));
    }

    [Fact]
    public void RequireHttpWwwPrefix_StillFindsPrefixedUrls()
    {
        Assert.NotEmpty(Detect(new Identifiers { Url = new Url() }, "see https://example.com/page here"));
    }

    // ---------------- defaults and round-trip ----------------

    [Fact]
    public void Defaults_MatchTheRedactionPolicySchema()
    {
        var creditCard = new CreditCard();
        Assert.True(creditCard.OnlyValidCreditCardNumbers);
        Assert.True(creditCard.OnlyWordBoundaries);
        Assert.False(creditCard.IgnoreWhenInUnixTimestamp);

        var email = new EmailAddress();
        Assert.True(email.OnlyStrictMatches);
        Assert.False(email.OnlyValidTLDs);

        var iban = new IbanCode();
        Assert.True(iban.AllowSpaces);
        Assert.True(iban.OnlyValidIBANCodes);

        var tracking = new TrackingNumber();
        Assert.True(tracking.Ups);
        Assert.True(tracking.Fedex);
        Assert.True(tracking.Usps);
        Assert.False(tracking.AllowSpaces);

        Assert.True(new Url().RequireHttpWwwPrefix);
        Assert.Equal(30, new PhEyeConfiguration().MaxIdleConnections);
    }

    [Theory]
    [InlineData("26-filter-options.json")]
    [InlineData("28-nested-options.json")]
    public void SpecExample_RoundTripsWithNoFieldLost(string file)
    {
        // Copies of the PhiSQL spec examples, vendored so the test does not depend on a sibling clone.
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "SpecExamples", file);
        Assert.True(File.Exists(path), $"missing test data: {path}");

        var original = File.ReadAllText(path);
        var round = PolicySerializer.SerializeToJson(PolicySerializer.DeserializeFromJson(original));

        var before = new List<string>();
        var after = new List<string>();
        Leaves(JsonDocument.Parse(original).RootElement, string.Empty, before);
        Leaves(JsonDocument.Parse(round).RootElement, string.Empty, after);

        var lost = before.Where(leaf => !after.Contains(leaf)).ToList();
        Assert.True(lost.Count == 0, "lost on round-trip: " + string.Join(", ", lost));
    }

    private static void Leaves(JsonElement element, string path, List<string> into)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    Leaves(property.Value, path + "/" + property.Name, into);
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                    Leaves(item, path + "/" + index++, into);
                break;
            default:
                into.Add(path + " = " + element);
                break;
        }
    }
}
