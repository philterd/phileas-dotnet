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

using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Strategies.Rules;
using Xunit;

namespace Phileas.Tests;

public class FpeEncryptionTests
{
    // A 256-bit (32-byte) AES key and an 8-byte tweak, both base64-encoded.
    private const string TestKeyBase64 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";  // 32 zero bytes
    private const string TestTweakBase64 = "AAAAAAAAAAA="; // 8 zero bytes

    private static Fpe CreateFpe() => new Fpe { Key = TestKeyBase64, Tweak = TestTweakBase64 };

    // ---------------------------------------------------------------------------
    // Ff1 unit-level tests (via the filter strategy layer)
    // ---------------------------------------------------------------------------

    [Fact]
    public void FpeStrategy_SsnInput_ReturnsDigitsAndHyphensOnly()
    {
        const string input = "123-45-6789";
        var strategy = new SsnFilterStrategy { Strategy = "FPE_ENCRYPT_REPLACE" };
        var fpe = CreateFpe();

        var replacement = strategy.GetReplacement("ctx", input, [], 1.0, null, null, null, fpe);

        // The encrypted SSN must preserve the format: digits and hyphens in same positions.
        Assert.Equal(input.Length, replacement.Value.Length);
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '-')
                Assert.Equal('-', replacement.Value[i]);
            else
                Assert.True(char.IsDigit(replacement.Value[i]),
                    $"Position {i}: expected digit, got '{replacement.Value[i]}'");
        }
    }

    [Fact]
    public void FpeStrategy_SsnInput_DifferentFromOriginal()
    {
        const string input = "123-45-6789";
        var strategy = new SsnFilterStrategy { Strategy = "FPE_ENCRYPT_REPLACE" };
        var fpe = CreateFpe();

        var replacement = strategy.GetReplacement("ctx", input, [], 1.0, null, null, null, fpe);

        // The encrypted value should differ from the plaintext (for this specific key/tweak).
        Assert.NotEqual(input, replacement.Value);
    }

    [Fact]
    public void FpeStrategy_SameInputAndKey_ProducesDeterministicOutput()
    {
        const string input = "123-45-6789";
        var strategy = new SsnFilterStrategy { Strategy = "FPE_ENCRYPT_REPLACE" };
        var fpe = CreateFpe();

        var r1 = strategy.GetReplacement("ctx", input, [], 1.0, null, null, null, fpe);
        var r2 = strategy.GetReplacement("ctx", input, [], 1.0, null, null, null, fpe);

        Assert.Equal(r1.Value, r2.Value);
    }

    [Fact]
    public void FpeStrategy_DifferentKeys_ProduceDifferentOutputs()
    {
        const string input = "123-45-6789";
        var strategy = new SsnFilterStrategy { Strategy = "FPE_ENCRYPT_REPLACE" };

        var fpe1 = new Fpe { Key = TestKeyBase64, Tweak = TestTweakBase64 };
        // Use a different key (all 1-bits).
        var fpe2 = new Fpe { Key = Convert.ToBase64String(new byte[32].Select(_ => (byte)0xFF).ToArray()), Tweak = TestTweakBase64 };

        var r1 = strategy.GetReplacement("ctx", input, [], 1.0, null, null, null, fpe1);
        var r2 = strategy.GetReplacement("ctx", input, [], 1.0, null, null, null, fpe2);

        Assert.NotEqual(r1.Value, r2.Value);
    }

    [Fact]
    public void FpeStrategy_NullFpe_FallsBackToRedaction()
    {
        const string input = "123-45-6789";
        var strategy = new SsnFilterStrategy { Strategy = "FPE_ENCRYPT_REPLACE" };

        var replacement = strategy.GetReplacement("ctx", input, [], 1.0, null, null, null, null);

        // Without FPE config the strategy must fall back to standard redaction.
        Assert.Contains("REDACTED", replacement.Value);
    }

    // ---------------------------------------------------------------------------
    // FilterService integration tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void FilterService_FpeStrategy_SsnIsFormPreserved()
    {
        var policy = new Phileas.Policy.Policy
        {
            Name = "fpe-test",
            Fpe = CreateFpe(),
            Identifiers = new Identifiers
            {
                Ssn = new Ssn
                {
                    Strategies = new List<Phileas.Policy.Filters.Strategies.SsnFilterStrategy>
                    {
                        new Phileas.Policy.Filters.Strategies.SsnFilterStrategy { Strategy = "FPE_ENCRYPT_REPLACE" }
                    }
                }
            }
        };

        const string input = "SSN: 123-45-6789";
        var result = FilterService.Filter(policy, "ctx", 0, input);

        // "SSN: " prefix must be unchanged.
        Assert.StartsWith("SSN: ", result.FilteredText);

        // The encrypted SSN must be 11 characters with hyphens at positions 3 and 6.
        var encrypted = result.FilteredText["SSN: ".Length..];
        Assert.Equal(11, encrypted.Length);
        Assert.Equal('-', encrypted[3]);
        Assert.Equal('-', encrypted[6]);
        for (int i = 0; i < encrypted.Length; i++)
        {
            if (i == 3 || i == 6) continue;
            Assert.True(char.IsDigit(encrypted[i]),
                $"Position {i}: expected digit, got '{encrypted[i]}'");
        }
    }

    [Fact]
    public void FilterService_FpeStrategy_EmailAddressIsFormPreserved()
    {
        var policy = new Phileas.Policy.Policy
        {
            Name = "fpe-test",
            Fpe = CreateFpe(),
            Identifiers = new Identifiers
            {
                EmailAddress = new EmailAddress
                {
                    Strategies = new List<Phileas.Policy.Filters.Strategies.EmailAddressFilterStrategy>
                    {
                        new Phileas.Policy.Filters.Strategies.EmailAddressFilterStrategy { Strategy = "FPE_ENCRYPT_REPLACE" }
                    }
                }
            }
        };

        const string input = "Email: user@example.com";
        var result = FilterService.Filter(policy, "ctx", 0, input);

        // The @ sign and dots must be preserved; only letters change.
        var encrypted = result.FilteredText["Email: ".Length..];
        Assert.Contains('@', encrypted);
        Assert.DoesNotContain("user", encrypted);
    }

    [Fact]
    public void FilterService_FpeStrategy_CreditCardIsFormPreserved()
    {
        var policy = new Phileas.Policy.Policy
        {
            Name = "fpe-test",
            Fpe = CreateFpe(),
            Identifiers = new Identifiers
            {
                CreditCard = new CreditCard
                {
                    Strategies = new List<Phileas.Policy.Filters.Strategies.CreditCardFilterStrategy>
                    {
                        new Phileas.Policy.Filters.Strategies.CreditCardFilterStrategy { Strategy = "FPE_ENCRYPT_REPLACE" }
                    }
                }
            }
        };

        const string input = "CC: 4111111111111111";
        var result = FilterService.Filter(policy, "ctx", 0, input);

        // All digits in the credit card number must remain digits.
        var encrypted = result.FilteredText["CC: ".Length..];
        Assert.Equal(16, encrypted.Length);
        Assert.True(encrypted.All(char.IsDigit), $"Expected all digits, got: {encrypted}");
        Assert.NotEqual("4111111111111111", encrypted);
    }
}
