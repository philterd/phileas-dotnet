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
using Phileas.Services;
using Phileas.Services.Disambiguation;
using Xunit;

namespace Phileas.Tests;

/// <summary>
///     A policy can decline span disambiguation with <c>config.analysis.spanDisambiguation</c>. The
///     field is declared by the redaction policy schema and was not bound, so setting it did nothing.
///     See philterd/phileas-dotnet#52.
/// </summary>
public class SpanDisambiguationPolicyTests
{
    /// <summary>Records whether the disambiguation step was reached, standing in for a host that enabled it.</summary>
    private sealed class SpyDisambiguationService : ISpanDisambiguationService
    {
        public int DisambiguateCalls { get; private set; }

        public void HashAndInsert(string context, Span span)
        {
        }

        public FilterType Disambiguate(string context, IList<FilterType> filterTypes, Span ambiguousSpan)
        {
            return filterTypes[0];
        }

        public IList<Span> Disambiguate(string context, IList<Span> spans)
        {
            DisambiguateCalls++;
            return spans;
        }
    }

    private const string Input = "ssn 078-05-1120 here";

    private static string PolicyJson(string? spanDisambiguation)
    {
        // spanDisambiguation sits under config.analysis, alongside identification.
        var analysis = spanDisambiguation == null
            ? "{\"identification\":true}"
            : "{\"identification\":true,\"spanDisambiguation\":" + spanDisambiguation + "}";

        return "{\"config\":{\"analysis\":" + analysis + "},\"identifiers\":{\"ssn\":{}}}";
    }

    private static int DisambiguationCalls(string? spanDisambiguation, ISpanDisambiguationService service)
    {
        var spy = service as SpyDisambiguationService;
        new FilterService(false, service).Filter(PolicySerializer.DeserializeFromJson(PolicyJson(spanDisambiguation)),
            "ctx", 0, Input);

        return spy?.DisambiguateCalls ?? 0;
    }

    // ---------------- the field binds ----------------

    [Fact]
    public void TheFieldDefaultsToTrue()
    {
        Assert.True(new Analysis().SpanDisambiguation);
        Assert.True(PolicySerializer.DeserializeFromJson(PolicyJson(null)).Config.Analysis.SpanDisambiguation);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void TheFieldDeserializes(string value, bool expected)
    {
        Assert.Equal(expected,
            PolicySerializer.DeserializeFromJson(PolicyJson(value)).Config.Analysis.SpanDisambiguation);
    }

    [Fact]
    public void TheFieldRoundTrips()
    {
        var json = PolicySerializer.SerializeToJson(PolicySerializer.DeserializeFromJson(PolicyJson("false")));

        Assert.Contains("\"spanDisambiguation\":false", json);
        Assert.False(PolicySerializer.DeserializeFromJson(json).Config.Analysis.SpanDisambiguation);
    }

    [Fact]
    public void APolicyUsingTheFieldValidatesAgainstTheSchema()
    {
        Assert.True(PolicySchema.Validate(PolicyJson("false")));
        Assert.True(PolicySchema.Validate(PolicyJson("true")));
    }

    // ---------------- the three combinations ----------------

    [Fact]
    public void PolicyOffAndHostOn_SkipsDisambiguation()
    {
        Assert.Equal(0, DisambiguationCalls("false", new SpyDisambiguationService()));
    }

    [Fact]
    public void PolicyAbsentAndHostOn_RunsDisambiguation()
    {
        Assert.Equal(1, DisambiguationCalls(null, new SpyDisambiguationService()));
    }

    [Fact]
    public void PolicyTrueAndHostOn_RunsDisambiguation()
    {
        Assert.Equal(1, DisambiguationCalls("true", new SpyDisambiguationService()));
    }

    [Fact]
    public void PolicyTrueAndHostOff_DoesNotDisambiguate()
    {
        // A policy cannot turn the feature on. The host expresses "off" by supplying a service that
        // does nothing, so the step is reached and changes nothing, which is indistinguishable from
        // not reaching it.
        var spans = new FilterService(false, new NoOpSpanDisambiguationService())
            .Filter(PolicySerializer.DeserializeFromJson(PolicyJson("true")), "ctx", 0, Input).Spans;

        Assert.Single(spans);
        Assert.Equal(FilterType.Ssn, spans[0].FilterType);
    }

    [Fact]
    public void APolicyWithNoAnalysisBlockGetsTheDefault()
    {
        // A policy built in code can leave the block off entirely. Reaching for the flag through it
        // must not be the thing that throws.
        var policy = PolicySerializer.DeserializeFromJson("{\"identifiers\":{\"ssn\":{}}}");
        policy.Config.Analysis = null!;

        var spy = new SpyDisambiguationService();
        var result = new FilterService(false, spy).Filter(policy, "ctx", 0, Input);

        Assert.Equal(1, spy.DisambiguateCalls);
        Assert.DoesNotContain("078-05-1120", result.FilteredText);
    }

    // ---------------- declining it does not change what is redacted ----------------

    [Theory]
    [InlineData(null)]
    [InlineData("true")]
    [InlineData("false")]
    public void TheDocumentIsStillRedactedEitherWay(string? spanDisambiguation)
    {
        // Disambiguation decides which type a contested span is, not whether it is redacted. Declining
        // it must not leave a value in the document.
        var filtered = new FilterService(false, new SpyDisambiguationService())
            .Filter(PolicySerializer.DeserializeFromJson(PolicyJson(spanDisambiguation)), "ctx", 0, Input)
            .FilteredText;

        Assert.DoesNotContain("078-05-1120", filtered);
    }
}
