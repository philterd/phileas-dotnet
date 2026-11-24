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

using System.Text.Json.Nodes;
using Phileas.Model;
using Phileas.Policy;
using Phileas.Services;
using Phileas.Services.Pdf;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
///     The policy's <c>config.review.samplingRate</c>, as specified in philterd/phisql#84. See
///     philterd/phileas-dotnet#215.
/// </summary>
public class ReviewSamplingTests
{
    private const string Input = "My email is test@example.com and my SSN is 123-45-6789.";

    private static string PolicyJson(string? review, string extraConfig = "") =>
        "{\"identifiers\":{\"emailAddress\":{},\"ssn\":{}},\"config\":{"
        + (review == null ? "" : "\"review\":" + review)
        + (review != null && extraConfig != "" ? "," : "") + extraConfig + "}}";

    private static PhileasPolicy Policy(string? review, string extraConfig = "") =>
        PolicySerializer.DeserializeFromJson(PolicyJson(review, extraConfig));

    private static PhileasPolicy Rate(double rate) =>
        Policy("{\"samplingRate\":" + rate.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");

    [Fact]
    public void SamplingRateDefaultsToZero()
    {
        Assert.Equal(0.0, Policy(null).Config.Review.SamplingRate);
        Assert.Equal(0.0, Policy("{}").Config.Review.SamplingRate);
        Assert.Equal(0.0, new PhileasPolicy().Config.Review.SamplingRate);
    }

    [Fact]
    public void SamplingRateRoundTrips()
    {
        var policy = Rate(0.1);
        Assert.Equal(0.1, policy.Config.Review.SamplingRate);

        var json = PolicySerializer.SerializeToJson(policy);
        Assert.Equal(0.1, JsonNode.Parse(json)!["config"]!["review"]!["samplingRate"]!.GetValue<double>());
        Assert.Equal(0.1, PolicySerializer.DeserializeFromJson(json).Config.Review.SamplingRate);
    }

    [Fact]
    public void SchemaAcceptsSamplingRate()
    {
        Assert.True(PolicySchema.Validate(PolicyJson("{\"samplingRate\":0.1}")));
        Assert.False(PolicySchema.Validate(PolicyJson("{\"samplingRate\":1.5}")));
    }

    [Theory]
    [InlineData("-0.1")]
    [InlineData("1.5")]
    public void SamplingRateOutsideZeroToOneIsALoadError(string rate)
    {
        var json = PolicyJson("{\"samplingRate\":" + rate + "}");

        Assert.Throws<PolicyValidationException>(() => PolicySerializer.DeserializeFromJson(json));

        // Refused without schema validation too, as in the Java port.
        var ex = Assert.Throws<PolicyValidationException>(
            () => PolicySerializer.DeserializeFromJson(json, validate: false));
        Assert.Contains("samplingRate", ex.Message);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    public void PolicyBuiltInCodeWithAnInvalidRateIsRefusedWhenFiltered(double rate)
    {
        var policy = Rate(0.0);
        policy.Config.Review.SamplingRate = rate;

        Assert.Throws<PolicyValidationException>(() => new FilterService().Filter(policy, "ctx", 0, Input));
    }

    [Fact]
    public void RateOfZeroNeverSelects()
    {
        var service = new FilterService();
        Assert.False(service.Filter(Policy(null), "ctx", 0, Input).ReviewSampled);
        for (var i = 0; i < 100; i++)
            Assert.False(service.Filter(Rate(0.0), "ctx", 0, Input).ReviewSampled);

        // Not drawn: a draw of 0.0 is below any rate above zero.
        Assert.False(ReviewSampler.IsSelected(Rate(0.0), () => 0.0));
    }

    [Fact]
    public void RateOfOneAlwaysSelects()
    {
        var service = new FilterService();
        for (var i = 0; i < 100; i++)
            Assert.True(service.Filter(Rate(1.0), "ctx", 0, Input).ReviewSampled);

        // Not drawn: the largest draw from [0, 1) is still below 1.0.
        Assert.True(ReviewSampler.IsSelected(Rate(1.0), () => Math.BitDecrement(1.0)));
    }

    [Fact]
    public void SelectedWhenTheDrawIsBelowTheRate()
    {
        var policy = Rate(0.25);
        Assert.True(ReviewSampler.IsSelected(policy, () => 0.0));
        Assert.True(ReviewSampler.IsSelected(policy, () => 0.2499));
        Assert.False(ReviewSampler.IsSelected(policy, () => 0.25));
        Assert.False(ReviewSampler.IsSelected(policy, () => 0.9));
    }

    [Fact]
    public void IntermediateRateIsAchievedOverManyCalls()
    {
        // Exercises the real random source, not a supplied draw.
        var policy = Rate(0.25);
        const int calls = 100_000;
        var selected = Enumerable.Range(0, calls).Count(_ => ReviewSampler.IsSelected(policy));

        // The standard deviation of the observed rate is about 0.0014, so 0.01 is a wide margin.
        Assert.InRange((double)selected / calls, 0.24, 0.26);
    }

    [Fact]
    public void NullReviewSettingNeverSelects()
    {
        // The schema refuses a null review object, so this is reachable only without validation.
        var policy = PolicySerializer.DeserializeFromJson(
            "{\"identifiers\":{\"ssn\":{}},\"config\":{\"review\":null}}", validate: false);

        Assert.False(ReviewSampler.IsSelected(policy));
        Assert.False(new FilterService().Filter(policy, "ctx", 0, Input).ReviewSampled);
    }

    [Theory]
    [InlineData("newline")]
    [InlineData("characters")]
    [InlineData("width")]
    public void SelectionSurvivesSplitting(string method)
    {
        var policy = Policy("{\"samplingRate\":1.0}",
            "\"splitting\":{\"enabled\":true,\"threshold\":10,\"method\":\"" + method + "\"}");

        Assert.True(new FilterService().Filter(policy, "ctx", 0, Input + "\n" + Input).ReviewSampled);
    }

    [Fact]
    public void SplittingDrawsOncePerRequest()
    {
        // At 0.5, a request drawn per piece across many pieces would be selected nearly every time.
        var policy = Policy("{\"samplingRate\":0.5}",
            "\"splitting\":{\"enabled\":true,\"threshold\":10,\"method\":\"newline\"}");
        var input = string.Join("\n", Enumerable.Repeat(Input, 20));
        var service = new FilterService();

        var sampled = Enumerable.Range(0, 200).Count(_ => service.Filter(policy, "ctx", 0, input).ReviewSampled);

        // A per-request draw gives about 100; per piece would give about 200. Outside 60 to 140 at 0.5 over
        // 200 calls is below 1 in 10^8.
        Assert.InRange(sampled, 60, 140);
    }

    [Fact]
    public void SelectionDoesNotChangeOutput()
    {
        var service = new FilterService();
        var notSampled = service.Filter(Rate(0.0), "ctx", 0, Input);
        var sampled = service.Filter(Rate(1.0), "ctx", 0, Input);

        Assert.False(notSampled.ReviewSampled);
        Assert.True(sampled.ReviewSampled);
        Assert.Equal("My email is {{{REDACTED-email-address}}} and my SSN is {{{REDACTED-ssn}}}.",
            sampled.FilteredText);
        AssertSameOutput(notSampled, sampled);
    }

    [Fact]
    public void DrawnSelectionDoesNotChangeOutput()
    {
        // Rates of 0.0 and 1.0 skip the draw, so an intermediate rate is what shows a draw leaves the output alone.
        var service = new FilterService();
        var baseline = service.Filter(Policy(null), "ctx", 0, Input);
        var policy = Rate(0.5);

        var sampled = 0;
        const int calls = 200;
        for (var i = 0; i < calls; i++)
        {
            var result = service.Filter(policy, "ctx", 0, Input);
            if (result.ReviewSampled)
                sampled++;
            AssertSameOutput(baseline, result);
        }

        // Both outcomes occur; the chance either is missing in 200 calls at 0.5 is about 1 in 10^60.
        Assert.InRange(sampled, 1, calls - 1);
    }

    [Fact]
    public void EvaluateUsesTheSameFilter()
    {
        // Evaluation filters through Filter, so a sampled policy evaluates the same as an unsampled one.
        var spans = new FilterService().Filter(Rate(0.0), "ctx", 0, Input).Spans;
        var result = new FilterService().Evaluate(Rate(1.0), "ctx", 0, Input, spans);
        Assert.Equal(1.0, result.Precision);
        Assert.Equal(1.0, result.Recall);
    }

    private static void AssertSameOutput(TextFilterResult expected, TextFilterResult actual)
    {
        Assert.Equal(expected.FilteredText, actual.FilteredText);
        Assert.Equal(expected.Tokens, actual.Tokens);
        Assert.Equal(expected.Context, actual.Context);
        Assert.Equal(expected.Spans.Select(Describe), actual.Spans.Select(Describe));
    }

    private static string Describe(Span span) =>
        $"{span.CharacterStart}-{span.CharacterEnd} {span.FilterType} {span.Text} {span.Replacement}";
}

/// <summary>Review sampling of a PDF: one draw for the document, whatever its page count.</summary>
[Collection("Pdf")]
public class PdfReviewSamplingTests
{
    private static byte[] TextPdf(int pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (var i = 0; i < pages; i++)
            builder.AddPage(612, 792).AddText("Patient SSN 123-45-6789 on file.", 12, new PdfPoint(72, 700), font);
        return builder.Build();
    }

    private static PhileasPolicy Rate(string rate) =>
        PolicySerializer.DeserializeFromJson(
            "{\"identifiers\":{\"ssn\":{}},\"config\":{\"review\":{\"samplingRate\":" + rate + "}}}");

    [Fact]
    public void PdfResultReportsSelectionWithoutChangingOutput()
    {
        var pdf = TextPdf(2);
        var service = new PdfFilterService();

        var notSampled = service.Filter(Rate("0.0"), "ctx", pdf, MimeType.ApplicationPdf);
        var sampled = service.Filter(Rate("1.0"), "ctx", pdf, MimeType.ApplicationPdf);

        Assert.False(notSampled.ReviewSampled);
        Assert.True(sampled.ReviewSampled);
        Assert.Equal(notSampled.Tokens, sampled.Tokens);
        Assert.Equal(notSampled.Spans.Select(s => (s.PageNumber, s.Text, s.Replacement)),
            sampled.Spans.Select(s => (s.PageNumber, s.Text, s.Replacement)));
    }
}
