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

using Phileas.Filters.Rules.Regex.RegexFilters;
using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
///     Credit card detection: shape first, issuer prefix and Luhn afterwards. Every number here is a
///     published test card, not an issued one. See philterd/phileas-dotnet#102.
/// </summary>
public class CreditCardBrandTests
{
    private static IList<Span> Detect(CreditCard creditCard, string input)
    {
        return new FilterService().Filter(
            new PhileasPolicy { Name = "t", Identifiers = new Identifiers { CreditCard = creditCard } },
            "ctx", 0, input).Spans;
    }

    [Theory]
    [InlineData("Visa", "4111111111111111")]
    [InlineData("Visa 13-digit", "4222222222222")]
    [InlineData("Mastercard 5-series", "5555555555554444")]
    [InlineData("Mastercard 2-series", "2223003122003222")] // the range that was missed entirely
    [InlineData("American Express", "378282246310005")]
    [InlineData("Discover", "6011111111111117")]
    [InlineData("Diners Club", "30569309025904")]
    [InlineData("JCB", "3530111333300000")]
    [InlineData("UnionPay", "6250947000000014")] // also missed before
    public void EveryPublishedTestCard_IsDetected(string brand, string number)
    {
        var spans = Detect(new CreditCard(), "card " + number + " end");

        var span = Assert.Single(spans);
        Assert.Equal(number, span.Text);
        Assert.Equal(FilterType.CreditCard, span.FilterType);
        Assert.True(brand.Length > 0);
    }

    [Theory]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("4111-1111-1111-1111")]
    [InlineData("5555 5555 5555 4444")]
    public void AFormattedCard_IsDetectedWhole(string formatted)
    {
        var span = Assert.Single(Detect(new CreditCard(), "card " + formatted + " end"));

        Assert.Equal(formatted, span.Text);
    }

    [Theory]
    [InlineData("1234567890123")] // no issuer prefix
    [InlineData("9999999999999999")]
    [InlineData("0000000000000000")]
    [InlineData("1612345678901")] // an epoch millisecond value
    [InlineData("4111111111111112")] // Visa prefix, fails Luhn
    [InlineData("2223003122003223")] // Mastercard 2-series prefix, fails Luhn
    public void ADigitRunThatIsNotACard_IsNotDetected(string value)
    {
        // Validation is both halves: a known issuer prefix and the checksum. Either alone is not
        // enough, which is what keeps the brand-agnostic pattern from being noisy by default.
        Assert.Empty(Detect(new CreditCard(), "value " + value + " end"));
    }

    [Fact]
    public void WithValidationOff_TheShapeAloneIsEnough()
    {
        // Documented consequence of detecting by shape: with the check disabled, any run of 13 to 16
        // digits is redacted. This is the precision cost the option now carries.
        var spans = Detect(new CreditCard { OnlyValidCreditCardNumbers = false },
            "order 1234567890123 end");

        Assert.Single(spans);
    }

    [Fact]
    public void NonAsciiDigits_AreNotACard_EvenWithValidationOff()
    {
        // .NET's \d spans every Unicode decimal digit, so the shape pattern spells out [0-9]. Without
        // that, a fullwidth-digit run is redacted as a card whenever validation is switched off.
        var fullwidth = new string("4111111111111111".Select(c => (char)('\uFF10' + (c - '0'))).ToArray());

        Assert.Empty(Detect(new CreditCard(), "card " + fullwidth + " end"));
        Assert.Empty(Detect(new CreditCard { OnlyValidCreditCardNumbers = false },
            "card " + fullwidth + " end"));
    }

    [Theory]
    [InlineData("ref=x4111111111111111y")] // letters either side, so no word boundary
    [InlineData("ID:5555555555554444.")] // punctuation either side, so the boundary holds
    public void WithoutWordBoundaries_TheSpanStillIndexesTheInput(string input)
    {
        // The lookahead form makes the match itself zero-width, so the span comes from capture
        // group 1. Its offsets still have to index the input.
        var span = Assert.Single(Detect(new CreditCard { OnlyWordBoundaries = false }, input));

        Assert.Equal(span.Text, input[span.CharacterStart..span.CharacterEnd]);
    }

    [Fact]
    public void OnlyWordBoundaries_IsWhatDecidesForACardInsideALongerToken()
    {
        // Punctuation is not a word character, so only letters or digits either side make the
        // difference; this is the case the option exists for.
        const string input = "ref=x4111111111111111y";

        Assert.Empty(Detect(new CreditCard(), input));
        Assert.Single(Detect(new CreditCard { OnlyWordBoundaries = false }, input));
    }

    [Fact]
    public void TwoAdjacentCards_AreTwoSpans()
    {
        var spans = Detect(new CreditCard(), "pay 4111111111111111 5555555555554444 now");

        Assert.Equal(2, spans.Count);
        Assert.Contains(spans, s => s.Text == "4111111111111111");
        Assert.Contains(spans, s => s.Text == "5555555555554444");
    }

    [Theory]
    [InlineData("9999 9999 9999 9995")]
    [InlineData("1000 0000 0000 0008")]
    public void AFormattedLuhnValidNumberWithNoIssuerPrefix_IsNotACard(string formatted)
    {
        // Deliberate change. The old filter had a second pattern matching any 16 digits written as
        // 4-4-4-4, validated by Luhn alone, so a formatted number with no issuer prefix was redacted
        // while the same digits unformatted were not. Validation is now the same either way: issuer
        // prefix and checksum. A policy that wants the old reach can set onlyValidCreditCardNumbers
        // to false, which keeps every run of 13 to 16 digits. See #102.
        Assert.Empty(Detect(new CreditCard(), "card " + formatted + " end"));
        Assert.Single(Detect(new CreditCard { OnlyValidCreditCardNumbers = false },
            "card " + formatted + " end"));
    }

    [Theory]
    [InlineData("3782 822463 10005")] // Amex, grouped 4-6-5
    [InlineData("3782-822463-10005")]
    [InlineData("4111 111111 111111")] // Visa, grouped 4-6-6
    [InlineData("3056 930902 5904")] // Diners, grouped 4-6-4
    public void ACardGroupedOtherThanFourByFour_IsNowDetected(string formatted)
    {
        // The removed pattern only understood 4-4-4-4, so these were missed.
        Assert.Single(Detect(new CreditCard(), "card " + formatted + " end"));
    }

    [Theory]
    [InlineData("411111111111", 0)] // 12 digits, below the window
    [InlineData("4222222222222", 1)] // 13, the shortest card
    [InlineData("4111111111111111", 1)] // 16, the longest
    [InlineData("41111111111111111", 0)] // 17, above the window
    [InlineData("4111111111111111111", 0)] // 19-digit cards are out of range, as in the Java filter
    public void TheDetectionWindowIsThirteenToSixteenDigits(string number, int expected)
    {
        Assert.Equal(expected, Detect(new CreditCard(), "card " + number + " end").Count);
    }

    [Fact]
    public void IsKnownBrand_AcceptsTheMastercardTwoSeriesRange()
    {
        // 2221 to 2720, opened in 2017 and absent from the old pattern.
        Assert.True(CreditCardFilter.IsKnownBrand("2221000000000009"));
        Assert.True(CreditCardFilter.IsKnownBrand("2720990000000004"));
        Assert.False(CreditCardFilter.IsKnownBrand("2220000000000000")); // below the range
        Assert.False(CreditCardFilter.IsKnownBrand("2721000000000000")); // above it
    }

    [Fact]
    public void IsKnownBrand_IgnoresSeparators()
    {
        Assert.True(CreditCardFilter.IsKnownBrand("4111 1111 1111 1111"));
        Assert.True(CreditCardFilter.IsKnownBrand("4111-1111-1111-1111"));
    }
}
