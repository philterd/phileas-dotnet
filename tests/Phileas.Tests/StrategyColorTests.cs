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
using Phileas.Policy.Filters.Strategies;
using Phileas.Services;
using Xunit;
using PolicyIdentifiers = Phileas.Policy.Identifiers;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
///     The per-strategy <c>color</c> property (schema 1.2.0) sets the PDF/image redaction-bar color for spans a
///     strategy redacts. It must load, flow to the detected span, and never change text redaction.
/// </summary>
public class StrategyColorTests
{
    private static PhileasPolicy SsnPolicy(string? color) => new()
    {
        Name = "ssn",
        Identifiers = new PolicyIdentifiers
        {
            Ssn = new Ssn { Strategies = new List<SsnFilterStrategy> { new() { Color = color } } }
        }
    };

    [Fact]
    public void ColoredStrategy_FlowsColorToTheDetectedSpan()
    {
        var result = new FilterService().Filter(SsnPolicy("red"), "ctx", 0, "SSN 123-45-6789.");

        var span = Assert.Single(result.Spans);
        Assert.Equal("red", span.Color);
    }

    [Fact]
    public void StrategyWithoutColor_LeavesSpanColorNull()
    {
        var result = new FilterService().Filter(SsnPolicy(null), "ctx", 0, "SSN 123-45-6789.");

        var span = Assert.Single(result.Spans);
        Assert.Null(span.Color);
    }

    [Fact]
    public void ColoredPolicy_DeserializesTheColorField()
    {
        const string json =
            "{\"identifiers\": {\"ssn\": {\"ssnFilterStrategies\": [{\"strategy\": \"REDACT\", \"color\": \"red\"}]}}}";

        var policy = PolicySerializer.DeserializeFromJson(json);

        var strategy = Assert.Single(policy.Identifiers.Ssn!.Strategies!);
        Assert.Equal("red", strategy.Color);
    }

    [Fact]
    public void ColoredPolicy_LoadsAndRedactsTextIdenticallyToTheUncoloredPolicy()
    {
        const string input = "SSN 123-45-6789 and 987-65-4321.";
        const string colored =
            "{\"identifiers\": {\"ssn\": {\"ssnFilterStrategies\": [{\"strategy\": \"REDACT\", \"color\": \"#ff8800\"}]}}}";
        const string plain =
            "{\"identifiers\": {\"ssn\": {\"ssnFilterStrategies\": [{\"strategy\": \"REDACT\"}]}}}";

        var coloredResult = new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(colored), "ctx", 0, input);
        var plainResult = new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(plain), "ctx", 0, input);

        // color is a PDF/image render concern only: the redacted text is byte-for-byte identical.
        Assert.Equal(plainResult.FilteredText, coloredResult.FilteredText);
    }
}
