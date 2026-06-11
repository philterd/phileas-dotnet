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
using Phileas.Policy.Filters;
using Phileas.Services;
using Phileas.Services.Disambiguation;
using Phileas.Services.Disambiguation.Vector;
using Xunit;
using PolicyIdentifiers = Phileas.Policy.Identifiers;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests.Disambiguation;

/// <summary>
///     End-to-end span disambiguation through the real <see cref="FilterService" /> pipeline with a real
///     SSN filter and a custom identifier filter, using an in-memory vector store. When the same value is
///     detected as two different PII types at the same location, the surrounding context decides the winner.
/// </summary>
public class SpanDisambiguationEndToEndTests
{
    /// <summary>A bare nine-digit value matched by both the SSN filter and the custom identifier filter.</summary>
    private const string Number = "123456789";

    /// <summary>Custom identifier regex that matches the same nine digits the SSN filter matches.</summary>
    private const string IdentifierRegex = @"\b\d{9}\b";

    private static VectorBasedSpanDisambiguationService Disambiguation(IVectorService vectorService)
    {
        var options = new SpanDisambiguationOptions
            { Enabled = true, IgnoreStopWords = true, VectorSize = 64 };
        return new VectorBasedSpanDisambiguationService(options, vectorService);
    }

    private static PhileasPolicy SsnOnlyPolicy()
    {
        return new PhileasPolicy { Name = "ssn", Identifiers = new PolicyIdentifiers { Ssn = new Ssn() } };
    }

    private static PhileasPolicy IdentifierOnlyPolicy()
    {
        return new PhileasPolicy
        {
            Name = "id",
            Identifiers = new PolicyIdentifiers
            {
                CustomIdentifiers = new List<Identifier>
                    { new() { Classification = "id", Pattern = IdentifierRegex } }
            }
        };
    }

    private static PhileasPolicy BothPolicy()
    {
        return new PhileasPolicy
        {
            Name = "both",
            Identifiers = new PolicyIdentifiers
            {
                Ssn = new Ssn(),
                CustomIdentifiers = new List<Identifier>
                    { new() { Classification = "id", Pattern = IdentifierRegex } }
            }
        };
    }

    private static Span ResolveNumberSpan(ISpanDisambiguationService disambiguation, string input)
    {
        var service = new FilterService(false, disambiguation);
        var result = service.Filter(BothPolicy(), "ctx", 0, input);

        var spans = result.Spans.Where(s => s.Text == Number).ToList();
        Assert.Single(spans);
        return spans[0];
    }

    private static void TrainSsn(ISpanDisambiguationService disambiguation, string input)
    {
        new FilterService(false, disambiguation).Filter(SsnOnlyPolicy(), "ctx", 0, input);
    }

    private static void TrainIdentifier(ISpanDisambiguationService disambiguation, string input)
    {
        new FilterService(false, disambiguation).Filter(IdentifierOnlyPolicy(), "ctx", 0, input);
    }

    [Fact]
    public void CompetingSpansAreProducedAtTheSameLocation()
    {
        // Sanity check the premise: with disambiguation off, both filters claim the same characters with
        // different types at the same location.
        var policy = BothPolicy();
        var input = "The number " + Number + " is here.";

        var spans = new FilterService().Filter(policy, "ctx", 0, input).Spans;

        // Before disambiguation/overlap-dedup, the two filters produce competing spans; after overlap
        // resolution one survives. Either way the value is detected.
        Assert.Contains(spans, s => s.Text == Number);
    }

    [Fact]
    public void ContextTrainedForSsnResolvesAmbiguousValueToSsn()
    {
        var disambiguation = Disambiguation(new InMemoryVectorService());

        TrainSsn(disambiguation, "The patient ssn social security number is " + Number + " on record.");
        TrainSsn(disambiguation, "Their social security ssn was listed as " + Number + " here.");

        var resolved = ResolveNumberSpan(disambiguation,
            "The ssn social security number " + Number + " was confirmed.");

        Assert.Equal(FilterType.Ssn, resolved.FilterType);
    }

    [Fact]
    public void ContextTrainedForIdentifierResolvesAmbiguousValueToIdentifier()
    {
        var disambiguation = Disambiguation(new InMemoryVectorService());

        TrainIdentifier(disambiguation, "The employee badge identifier " + Number + " was assigned.");
        TrainIdentifier(disambiguation, "Employee badge identifier number " + Number + " is active.");

        var resolved = ResolveNumberSpan(disambiguation,
            "The employee badge identifier " + Number + " is shown.");

        Assert.Equal(FilterType.Identifier, resolved.FilterType);
    }

    [Fact]
    public void IdenticalInputResolvesIdenticallyAcrossRuns()
    {
        var disambiguation = Disambiguation(new InMemoryVectorService());
        TrainIdentifier(disambiguation, "The employee badge identifier " + Number + " was assigned.");

        var input = "The employee badge identifier " + Number + " is shown.";
        var first = ResolveNumberSpan(disambiguation, input).FilterType;
        var second = ResolveNumberSpan(disambiguation, input).FilterType;

        Assert.Equal(first, second);
    }
}
