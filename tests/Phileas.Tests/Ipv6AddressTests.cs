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
///     IPv6 address forms and the boundaries around them. Every address here is documentation or
///     link-local range, not a routable host. See philterd/phileas-dotnet#76.
/// </summary>
public class Ipv6AddressTests
{
    private static IList<Span> Detect(string input)
    {
        return new FilterService().Filter(
            new PhileasPolicy { Name = "t", Identifiers = new Identifiers { IpAddress = new IpAddress() } },
            "ctx", 0, input).Spans;
    }

    [Theory]
    [InlineData("FE80::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2001:db8:85a3::8a2e:370:7334")]
    [InlineData("::1")]
    [InlineData("::ffff:192.0.2.128")]
    [InlineData("fe80::1%eth0")] // with a zone identifier
    [InlineData("2001:db8::")] // trailing compression
    [InlineData("::ffff:0:192.0.2.128")]
    [InlineData("fe80::200:5aee:feaa:20a2")]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334")] // expanded, eight groups
    public void AnIpv6Address_IsOneSpanCoveringTheWholeAddress(string address)
    {
        var input = "addr " + address + " end";

        var span = Assert.Single(Detect(input));
        Assert.Equal(address, span.Text);
        Assert.Equal(address, input[span.CharacterStart..span.CharacterEnd]);
        Assert.Equal(FilterType.IpAddress, span.FilterType);
    }

    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.255")]
    public void AnIpv4Address_IsStillExactlyOneSpan(string address)
    {
        var span = Assert.Single(Detect("addr " + address + " end"));

        Assert.Equal(address, span.Text);
        Assert.Equal(0.95, span.Confidence);
    }

    [Theory]
    [InlineData("v1.2.3.4", "1.2.3.4")]
    [InlineData("abc192.168.1.1def", "192.168.1.1")]
    [InlineData("file10.0.0.1.txt", "10.0.0.1")]
    public void AnIpv4AddressAbuttingText_IsDetectedAtALowerConfidence(string input, string address)
    {
        // Still redacted, because it may well be an address, but it is as likely to be a version
        // string or an identifier, so the confidence reflects that for span disambiguation.
        var span = Assert.Single(Detect("x " + input + " y"));

        Assert.Equal(address, span.Text);
        Assert.Equal(0.70, span.Confidence);
    }

    [Theory]
    [InlineData("build-10.0.0.1-rc", "10.0.0.1")] // hyphens delimit, so the boundary is clean
    [InlineData("version 1.2.3.4 released", "1.2.3.4")]
    public void AnIpv4AddressCleanlyDelimited_KeepsTheHigherConfidence(string input, string address)
    {
        var span = Assert.Single(Detect("x " + input + " y"));

        Assert.Equal(address, span.Text);
        Assert.Equal(0.95, span.Confidence);
    }

    [Theory]
    [InlineData("12:30")] // a time
    [InlineData("00:1A:2B:3C:4D:5E")] // a MAC address
    [InlineData("a1:b2")]
    public void SomethingThatIsNotAnAddress_GainsNoSpan(string value)
    {
        Assert.Empty(Detect("value " + value + " end"));
    }

    [Theory]
    [InlineData("std::vector")] // C++
    [InlineData("Foo::Bar")]
    [InlineData("Model::where")] // PHP
    [InlineData("x::y")]
    [InlineData("a :: b")]
    [InlineData("Haskell x :: Int")]
    [InlineData("path/to::thing")]
    public void ADoubleColonInProseOrCode_GainsNoSpan(string value)
    {
        // A bare "::" is the unspecified address, and accepting it meant every "::" in a technical
        // document became a span, often a ragged one: "std::vector" produced "d::" and "Foo::Bar"
        // produced "::Ba".
        Assert.Empty(Detect("value " + value + " end"));
    }

    [Fact]
    public void AnAddressInsideNonLatinText_IsStillFound()
    {
        // The boundary class is ASCII, so surrounding script does not suppress the match.
        const string input = "アドレス 2001:db8::1 です";

        var span = Assert.Single(Detect(input));
        Assert.Equal("2001:db8::1", span.Text);
    }

    [Fact]
    public void NonAsciiDigits_AreNotAnAddress()
    {
        var fullwidth = new string("2001".Select(c => (char)('０' + (c - '0'))).ToArray()) + ":db8::1";

        Assert.Empty(Detect("addr " + fullwidth + " end"));
    }

    [Fact]
    public void AnAddressEndingASentence_ExcludesTheTrailingPeriod()
    {
        var span = Assert.Single(Detect("The host is 2001:db8::1."));

        Assert.Equal("2001:db8::1", span.Text);
    }

    [Fact]
    public void SeveralAddressesInOneDocument_AreSeparateSpans()
    {
        var spans = Detect("from 2001:db8::1 to fe80::2 via 192.168.1.1");

        Assert.Equal(3, spans.Count);
        Assert.Contains(spans, s => s.Text == "2001:db8::1");
        Assert.Contains(spans, s => s.Text == "fe80::2");
        Assert.Contains(spans, s => s.Text == "192.168.1.1");
    }
}
