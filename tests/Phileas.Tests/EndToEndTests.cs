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

using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
/// End-to-end tests that build a policy with various filters, apply the policy to text
/// paragraphs containing fake PII, and assert that the expected redactions occur.
/// </summary>
public class EndToEndTests
{
    // -------------------------------------------------------------------------
    // Multi-filter policy
    // -------------------------------------------------------------------------

    private static PhileasPolicy BuildMultiFilterPolicy() =>
        new PhileasPolicy
        {
            Name = "e2e-multi",
            Identifiers = new Identifiers
            {
                EmailAddress = new EmailAddress(),
                Ssn = new Ssn(),
                PhoneNumber = new PhoneNumber(),
                CreditCard = new CreditCard(),
                IpAddress = new IpAddress(),
                ZipCode = new ZipCode()
            }
        };

    [Fact]
    public void EndToEnd_MultiFilter_RedactsEmailInParagraph()
    {
        const string input =
            "Dear John, please contact our support team at support@example.com for any questions. " +
            "We look forward to hearing from you.";

        var result = new FilterService().Filter(BuildMultiFilterPolicy(), "ctx", 0, input);

        Assert.DoesNotContain("support@example.com", result.FilteredText);
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.Contains(result.Spans, s => s.FilterType == FilterType.EmailAddress);
    }

    [Fact]
    public void EndToEnd_MultiFilter_RedactsSsnInParagraph()
    {
        const string input =
            "Patient Jane Doe has been assigned SSN 123-45-6789 for billing purposes. " +
            "Please keep this information confidential.";

        var result = new FilterService().Filter(BuildMultiFilterPolicy(), "ctx", 0, input);

        Assert.DoesNotContain("123-45-6789", result.FilteredText);
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.Contains(result.Spans, s => s.FilterType == FilterType.Ssn);
    }

    [Fact]
    public void EndToEnd_MultiFilter_RedactsPhoneNumberInParagraph()
    {
        const string input =
            "You can reach our office at 555-867-5309 between 9 AM and 5 PM on weekdays.";

        var result = new FilterService().Filter(BuildMultiFilterPolicy(), "ctx", 0, input);

        Assert.DoesNotContain("555-867-5309", result.FilteredText);
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.Contains(result.Spans, s => s.FilterType == FilterType.PhoneNumber);
    }

    [Fact]
    public void EndToEnd_MultiFilter_RedactsCreditCardInParagraph()
    {
        const string input =
            "The payment was processed using card number 4111111111111111. " +
            "Please do not share this number with anyone.";

        var result = new FilterService().Filter(BuildMultiFilterPolicy(), "ctx", 0, input);

        Assert.DoesNotContain("4111111111111111", result.FilteredText);
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.Contains(result.Spans, s => s.FilterType == FilterType.CreditCard);
    }

    [Fact]
    public void EndToEnd_MultiFilter_RedactsIpAddressInParagraph()
    {
        const string input =
            "The request originated from IP address 192.168.1.100 at 3:45 PM on Tuesday.";

        var result = new FilterService().Filter(BuildMultiFilterPolicy(), "ctx", 0, input);

        Assert.DoesNotContain("192.168.1.100", result.FilteredText);
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.Contains(result.Spans, s => s.FilterType == FilterType.IpAddress);
    }

    [Fact]
    public void EndToEnd_MultiFilter_RedactsZipCodeInParagraph()
    {
        const string input =
            "Please mail your documents to 123 Main Street, Springfield, 62704.";

        var result = new FilterService().Filter(BuildMultiFilterPolicy(), "ctx", 0, input);

        Assert.DoesNotContain("62704", result.FilteredText);
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.Contains(result.Spans, s => s.FilterType == FilterType.ZipCode);
    }

    [Fact]
    public void EndToEnd_MultiFilter_RedactsAllPiiInLongParagraph()
    {
        const string email = "jsmith@fakecorp.net";
        const string ssn = "234-56-7890";
        const string phone = "800-555-0199";
        const string creditCard = "5500005555555559";
        const string ip = "10.0.0.42";

        var input =
            $"Hello, my name is John Smith. You can email me at {email}. " +
            $"My social security number is {ssn}. " +
            $"Call me at {phone} if you need to speak by phone. " +
            $"My credit card is {creditCard}. " +
            $"I connected from the address {ip} this morning.";

        var result = new FilterService().Filter(BuildMultiFilterPolicy(), "ctx", 0, input);

        Assert.DoesNotContain(email, result.FilteredText);
        Assert.DoesNotContain(ssn, result.FilteredText);
        Assert.DoesNotContain(phone, result.FilteredText);
        Assert.DoesNotContain(creditCard, result.FilteredText);
        Assert.DoesNotContain(ip, result.FilteredText);

        Assert.Contains(result.Spans, s => s.FilterType == FilterType.EmailAddress);
        Assert.Contains(result.Spans, s => s.FilterType == FilterType.Ssn);
        Assert.Contains(result.Spans, s => s.FilterType == FilterType.PhoneNumber);
        Assert.Contains(result.Spans, s => s.FilterType == FilterType.CreditCard);
        Assert.Contains(result.Spans, s => s.FilterType == FilterType.IpAddress);
    }

    [Fact]
    public void EndToEnd_MultiFilter_PreservesNonPiiText()
    {
        const string input = "The weather today is sunny and warm with no chance of rain.";

        var result = new FilterService().Filter(BuildMultiFilterPolicy(), "ctx", 0, input);

        Assert.Equal(input, result.FilteredText);
        Assert.Empty(result.Spans);
    }

    // -------------------------------------------------------------------------
    // Single-filter policies – targeted redaction assertions
    // -------------------------------------------------------------------------

    [Fact]
    public void EndToEnd_EmailOnlyPolicy_DoesNotRedactPhoneNumber()
    {
        var policy = new PhileasPolicy
        {
            Name = "email-only",
            Identifiers = new Identifiers { EmailAddress = new EmailAddress() }
        };

        const string input = "Email me at user@example.com or call 555-123-4567.";

        var result = new FilterService().Filter(policy, "ctx", 0, input);

        Assert.DoesNotContain("user@example.com", result.FilteredText);
        Assert.Contains("555-123-4567", result.FilteredText);
        Assert.Single(result.Spans);
        Assert.Equal(FilterType.EmailAddress, result.Spans[0].FilterType);
    }

    [Fact]
    public void EndToEnd_SsnOnlyPolicy_DoesNotRedactEmail()
    {
        var policy = new PhileasPolicy
        {
            Name = "ssn-only",
            Identifiers = new Identifiers { Ssn = new Ssn() }
        };

        const string input = "Contact admin@example.org. SSN on file: 321-54-9876.";

        var result = new FilterService().Filter(policy, "ctx", 0, input);

        Assert.Contains("admin@example.org", result.FilteredText);
        Assert.DoesNotContain("321-54-9876", result.FilteredText);
        Assert.Single(result.Spans);
        Assert.Equal(FilterType.Ssn, result.Spans[0].FilterType);
    }

    [Fact]
    public void EndToEnd_UrlOnlyPolicy_RedactsUrl()
    {
        var policy = new PhileasPolicy
        {
            Name = "url-only",
            Identifiers = new Identifiers { Url = new Url() }
        };

        const string input = "Visit our site at https://www.example.com for more info.";

        var result = new FilterService().Filter(policy, "ctx", 0, input);

        Assert.DoesNotContain("https://www.example.com", result.FilteredText);
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.NotEmpty(result.Spans);
        Assert.All(result.Spans, s => Assert.Equal(FilterType.Url, s.FilterType));
    }

    // -------------------------------------------------------------------------
    // Span accuracy assertions
    // -------------------------------------------------------------------------

    [Fact]
    public void EndToEnd_SpanPositions_AreAccurateForEmail()
    {
        var policy = new PhileasPolicy
        {
            Name = "span-check",
            Identifiers = new Identifiers { EmailAddress = new EmailAddress() }
        };

        const string input = "Email: user@example.com end";
        // "user@example.com" starts at index 7; CharacterEnd is exclusive, so 7 + 16 = 23

        var result = new FilterService().Filter(policy, "ctx", 0, input);

        Assert.Single(result.Spans);
        var span = result.Spans[0];
        Assert.Equal("user@example.com", span.Text);
        Assert.Equal(7, span.CharacterStart);
        Assert.Equal(23, span.CharacterEnd);
        Assert.Equal(FilterType.EmailAddress, span.FilterType);
    }

    [Fact]
    public void EndToEnd_MultipleOccurrences_AllRedacted()
    {
        var policy = new PhileasPolicy
        {
            Name = "multi-occur",
            Identifiers = new Identifiers { EmailAddress = new EmailAddress() }
        };

        const string input = "Contact a@example.com or b@example.com for support.";

        var result = new FilterService().Filter(policy, "ctx", 0, input);

        Assert.DoesNotContain("a@example.com", result.FilteredText);
        Assert.DoesNotContain("b@example.com", result.FilteredText);
        Assert.Equal(2, result.Spans.Count);
        Assert.All(result.Spans, s => Assert.Equal(FilterType.EmailAddress, s.FilterType));
    }

    // -------------------------------------------------------------------------
    // Policy built from serialised JSON – round-trip end-to-end
    // -------------------------------------------------------------------------

    [Fact]
    public void EndToEnd_PolicyFromJson_RedactsExpectedPii()
    {
        const string json = """
        {
            "name": "json-policy",
            "identifiers": {
                "emailAddress": {},
                "ssn": {}
            }
        }
        """;

        var policy = System.Text.Json.JsonSerializer.Deserialize<PhileasPolicy>(json)!;

        const string input = "Reach Jane at jane.doe@example.com. Her SSN is 123-45-6789.";

        var result = new FilterService().Filter(policy, "ctx", 0, input);

        Assert.DoesNotContain("jane.doe@example.com", result.FilteredText);
        Assert.DoesNotContain("123-45-6789", result.FilteredText);
        Assert.Equal(2, result.Spans.Count);
    }
}
