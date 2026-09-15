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
using Phileas.Policy.Filters;
using Phileas.Services;
using Phileas.Services.Validators;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
///     The aba, verhoeff and damm validators. See philterd/phileas-dotnet#89.
/// </summary>
public class ChecksumValidatorTests
{
    // 021000021 is a real routing number for a financial institution, not personal data, and is
    // already the fixture used by BankRoutingNumberFilterTests.
    [Theory]
    [InlineData("021000021")]
    [InlineData("011000015")]
    [InlineData("021 000 021")] // separators are ignored
    public void Aba_AcceptsAValidRoutingNumber(string value)
    {
        Assert.True(AbaValidator.IsValid(value));
    }

    [Theory]
    [InlineData("021000022")] // last digit altered
    [InlineData("021000031")] // an interior digit altered
    [InlineData("012000021")] // first two digits transposed
    public void Aba_RejectsAnInvalidChecksum(string value)
    {
        Assert.False(AbaValidator.IsValid(value));
    }

    [Theory]
    [InlineData("02100002")] // eight digits
    [InlineData("0210000211")] // ten digits
    [InlineData("")]
    [InlineData("no digits here")]
    public void Aba_RejectsAnythingThatIsNotNineDigits(string value)
    {
        Assert.False(AbaValidator.IsValid(value));
    }

    [Theory]
    [InlineData("2363")]
    [InlineData("123451")]
    [InlineData("758722")]
    public void Verhoeff_AcceptsAValidValue(string value)
    {
        Assert.True(VerhoeffValidator.IsValid(value));
    }

    [Theory]
    [InlineData("2364")] // check digit altered
    [InlineData("12345")] // the body without its check digit
    [InlineData("3263")] // first two digits transposed
    [InlineData("")]
    public void Verhoeff_RejectsAnInvalidValue(string value)
    {
        Assert.False(VerhoeffValidator.IsValid(value));
    }

    [Theory]
    [InlineData("5724")]
    [InlineData("112946")]
    public void Damm_AcceptsAValidValue(string value)
    {
        Assert.True(DammValidator.IsValid(value));
    }

    [Theory]
    [InlineData("5727")] // check digit altered
    [InlineData("112949")]
    [InlineData("5742")] // adjacent digits transposed
    [InlineData("")]
    public void Damm_RejectsAnInvalidValue(string value)
    {
        Assert.False(DammValidator.IsValid(value));
    }

    [Fact]
    public void BothSchemes_AcceptExactlyOneCheckDigitPerValue()
    {
        // The defining property of a check-digit scheme, and the thing a transcribed table gets
        // wrong: for any body exactly one appended digit may validate.
        for (var n = 0; n < 500; n++)
        {
            var body = n.ToString();
            Assert.Equal(1, Enumerable.Range(0, 10).Count(d => VerhoeffValidator.IsValid(body + d)));
            Assert.Equal(1, Enumerable.Range(0, 10).Count(d => DammValidator.IsValid(body + d)));
        }
    }

    [Fact]
    public void BothSchemes_CatchEveryAdjacentTransposition()
    {
        // This is why these schemes are used in place of a plain modulus check.
        for (var n = 1000; n < 1100; n++)
        {
            var body = n.ToString();
            var verhoeff = body + Enumerable.Range(0, 10).First(d => VerhoeffValidator.IsValid(body + d));
            var damm = body + Enumerable.Range(0, 10).First(d => DammValidator.IsValid(body + d));

            AssertTranspositionsRejected(verhoeff, VerhoeffValidator.IsValid);
            AssertTranspositionsRejected(damm, DammValidator.IsValid);
        }
    }

    private static void AssertTranspositionsRejected(string value, Func<string, bool> isValid)
    {
        for (var i = 0; i < value.Length - 1; i++)
        {
            if (value[i] == value[i + 1]) continue;

            var swapped = value.ToCharArray();
            (swapped[i], swapped[i + 1]) = (swapped[i + 1], swapped[i]);
            Assert.False(isValid(new string(swapped)), $"{value} transposed at {i} was accepted");
        }
    }

    [Theory]
    // pattern, validator, a value the checksum accepts, a value it rejects
    [InlineData(@"\b[0-9]{9}\b", "aba", "021000021", "021000022")]
    [InlineData(@"\b[0-9]{6}\b", "verhoeff", "758722", "758723")]
    [InlineData(@"\b[0-9]{6}\b", "damm", "112946", "112947")]
    public void ValidatorNamedInAPolicy_KeepsValidValuesAndDropsInvalidOnes(
        string pattern, string validator, string valid, string invalid)
    {
        var policy = new PhileasPolicy
        {
            Name = "t",
            Identifiers = new Identifiers
            {
                CustomIdentifiers = new List<Identifier>
                {
                    new()
                    {
                        Classification = "ACCOUNT",
                        Pattern = pattern,
                        Validator = new Validator { Name = validator }
                    }
                }
            }
        };

        var result = new FilterService().Filter(policy, "ctx", 0,
            $"good {valid} and bad {invalid} end");

        // The validator has to run, not merely resolve: only the value passing the checksum is a span.
        var span = Assert.Single(result.Spans);
        Assert.Equal(valid, span.Text);
        Assert.Contains(invalid, result.FilteredText);
        Assert.DoesNotContain(valid, result.FilteredText);
    }

    [Theory]
    [InlineData("aba")]
    [InlineData("verhoeff")]
    [InlineData("damm")]
    public void EachValidator_ResolvesByName(string name)
    {
        var validator = IdentifierValidators.FromPolicy(new Validator { Name = name });

        Assert.NotNull(validator);
    }

    [Fact]
    public void AnUnknownName_IsStillARejectedPolicy()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => IdentifierValidators.FromPolicy(new Validator { Name = "not-a-validator" }));

        Assert.Contains("aba", ex.Message);
        Assert.Contains("verhoeff", ex.Message);
        Assert.Contains("damm", ex.Message);
    }
}
