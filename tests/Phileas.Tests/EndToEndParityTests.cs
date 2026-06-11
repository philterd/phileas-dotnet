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

using System.Security.Cryptography;
using System.Text;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;
using Phileas.Services;
using Xunit;
using PolicyIdentifiers = Phileas.Policy.Identifiers;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
///     End-to-end parity cases ported from the Java <c>EndToEndTests</c> and
///     <c>EndToEndWithIncrementalRedactionsTest</c> (PDF and vector-disambiguation cases excluded).
/// </summary>
public class EndToEndParityTests
{
    private static PhileasPolicy EmailAndCreditCardPolicy()
    {
        return new PhileasPolicy
        {
            Identifiers = new PolicyIdentifiers
            {
                EmailAddress = new EmailAddress
                    { Strategies = new List<EmailAddressFilterStrategy> { new() } },
                CreditCard = new CreditCard { Strategies = new List<CreditCardFilterStrategy> { new() } }
            }
        };
    }

    private static PhileasPolicy SsnAndZipCodePolicy(bool splitting = false)
    {
        var policy = new PhileasPolicy
        {
            Identifiers = new PolicyIdentifiers
            {
                Ssn = new Ssn { Strategies = new List<SsnFilterStrategy> { new() } },
                ZipCode = new ZipCode { Strategies = new List<ZipCodeFilterStrategy> { new() } }
            }
        };

        if (splitting)
        {
            policy.Config.Splitting = new Splitting { Enabled = true, Threshold = 100, Method = "newline" };
        }

        return policy;
    }

    private static string Sha256Hex(string text)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    [Fact]
    public void EndToEnd2_EmailAndCreditCard()
    {
        var response = new FilterService().Filter(EmailAndCreditCardPolicy(), "context", 0,
            "My email is test@something.com and cc is 4121742025464465");

        Assert.Equal("My email is {{{REDACTED-email-address}}} and cc is {{{REDACTED-credit-card}}}",
            response.FilteredText);
    }

    [Fact]
    public void EndToEnd3_EmailAtStart()
    {
        var response = new FilterService().Filter(EmailAndCreditCardPolicy(), "context", 0,
            "test@something.com is email and cc is 4121742025464465");

        Assert.Equal("{{{REDACTED-email-address}}} is email and cc is {{{REDACTED-credit-card}}}",
            response.FilteredText);
    }

    [Fact]
    public void EndToEnd4_WholeInputIsEntity()
    {
        var response = new FilterService().Filter(EmailAndCreditCardPolicy(), "context", 0, "test@something.com");

        Assert.Equal("{{{REDACTED-email-address}}}", response.FilteredText);
    }

    [Fact]
    public void EndToEndWithOverlappingSpans_LongestMaskWins()
    {
        var policy = new PhileasPolicy
        {
            Identifiers = new PolicyIdentifiers
            {
                BitcoinAddress = new BitcoinAddress
                {
                    Strategies = new List<BitcoinAddressFilterStrategy>
                        { new() { Strategy = "MASK", MaskCharacter = "*", MaskLength = "same" } }
                },
                CreditCard = new CreditCard
                {
                    Strategies = new List<CreditCardFilterStrategy>
                        { new() { Strategy = "MASK", MaskCharacter = "*", MaskLength = "same" } }
                },
                DriversLicense = new DriversLicense
                {
                    Strategies = new List<DriversLicenseFilterStrategy>
                        { new() { Strategy = "MASK", MaskCharacter = "*", MaskLength = "same" } }
                }
            }
        };

        var response = new FilterService().Filter(policy, "context", 0,
            "the payment method is 4532613702852251 visa or 1Lbcfr7sAHTD9CgdQo3HTMTkV8LK4ZnX71 BTC from user.");

        Assert.Equal(2, response.Spans.Count);
        Assert.Equal(
            "the payment method is **************** visa or ********************************** BTC from user.",
            response.FilteredText);
    }

    [Fact]
    public void EndToEndWithRedactionIncrements()
    {
        var response = new FilterService(incrementalRedactionsEnabled: true).Filter(SsnAndZipCodePolicy(),
            "context", 0,
            "George Washington whose SSN was 123-45-6789 was the first president of the United States and he lived at 90210.");

        Assert.Equal(
            "George Washington whose SSN was {{{REDACTED-ssn}}} was the first president of the United States and he lived at {{{REDACTED-zip-code}}}.",
            response.FilteredText);
        Assert.NotEmpty(response.IncrementalRedactions);

        foreach (var redaction in response.IncrementalRedactions)
        {
            Assert.Equal(Sha256Hex(redaction.IncrementallyRedactedText), redaction.Hash);
        }
    }

    [Fact]
    public void EndToEndWithoutRedactionIncrements()
    {
        var response = new FilterService(incrementalRedactionsEnabled: true).Filter(SsnAndZipCodePolicy(),
            "context", 0, "George Washington was president.");

        Assert.Equal("George Washington was president.", response.FilteredText);
        Assert.Empty(response.IncrementalRedactions);
    }

    [Fact]
    public void EndToEndWithSplitsAndIncrements()
    {
        var response = new FilterService(incrementalRedactionsEnabled: true).Filter(
            SsnAndZipCodePolicy(splitting: true), "context", 0,
            "George Washington whose SSN was 123-45-6789 was\n the first president of the United States and he lived at 90210.\nThe second president was John Adams. Abraham Lincoln was later on. His SSN was 123-45-6789.");

        var newline = Environment.NewLine;
        Assert.Equal(
            "George Washington whose SSN was {{{REDACTED-ssn}}} was" + newline
            + "the first president of the United States and he lived at {{{REDACTED-zip-code}}}." + newline
            + "The second president was John Adams. Abraham Lincoln was later on. His SSN was {{{REDACTED-ssn}}}.",
            response.FilteredText);

        foreach (var redaction in response.IncrementalRedactions)
        {
            Assert.Equal(Sha256Hex(redaction.IncrementallyRedactedText), redaction.Hash);
        }
    }
}
