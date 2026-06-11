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
using Phileas.Services;
using Xunit;
using PolicyIdentifiers = Phileas.Policy.Identifiers;
using PhileasPolicy = Phileas.Policy.Policy;
using PolicySsnStrategy = Phileas.Policy.Filters.Strategies.SsnFilterStrategy;

namespace Phileas.Tests;

/// <summary>
///     Replacement-placement and incremental-redaction behavior of the filtering pipeline. Mirrors the
///     Java <c>UnstructuredDocumentProcessorReplacementTest</c>, plus splitting integration.
/// </summary>
public class DocumentProcessingTests
{
    /// <summary>An SSN policy that statically replaces each SSN with a single short token.</summary>
    private static PhileasPolicy ShortReplacementSsnPolicy()
    {
        return new PhileasPolicy
        {
            Identifiers = new PolicyIdentifiers
            {
                Ssn = new Ssn
                {
                    Strategies = new List<PolicySsnStrategy>
                    {
                        new() { Strategy = "STATIC_REPLACE", StaticReplacement = "#" }
                    }
                }
            }
        };
    }

    private static string Sha256Hex(string text)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    [Fact]
    public void MultipleShrinkingReplacementsArePlacedCorrectly()
    {
        // Each SSN (11 chars) becomes "#" (1 char), so the running offset goes increasingly negative.
        // A wrong offset would misplace the second and third replacements.
        var result = new FilterService().Filter(ShortReplacementSsnPolicy(), "context", 0,
            "A 123-45-6789 B 234-56-7890 C 345-67-8901 D");

        Assert.Equal("A # B # C # D", result.FilteredText);
    }

    [Fact]
    public void IncrementalRedactionHashesMatchAfterShrinkingReplacements()
    {
        var result = new FilterService(incrementalRedactionsEnabled: true)
            .Filter(ShortReplacementSsnPolicy(), "context", 0, "A 123-45-6789 B 234-56-7890 C");

        Assert.Equal("A # B # C", result.FilteredText);
        Assert.NotEmpty(result.IncrementalRedactions);

        // Each incremental snapshot must hash to its recorded hash, and they progress left-to-right.
        foreach (var redaction in result.IncrementalRedactions)
        {
            Assert.Equal(Sha256Hex(redaction.IncrementallyRedactedText), redaction.Hash);
        }

        // First increment redacts the first SSN only; the final increment equals the filtered text.
        Assert.Equal("A # B 234-56-7890 C", result.IncrementalRedactions[0].IncrementallyRedactedText);
        Assert.Equal("A # B # C", result.IncrementalRedactions[^1].IncrementallyRedactedText);
    }

    [Fact]
    public void TokensAreCounted()
    {
        var result = new FilterService().Filter(ShortReplacementSsnPolicy(), "context", 0, "A 123-45-6789 B");

        Assert.Equal(3, result.Tokens);
    }

    [Fact]
    public void SplittingFiltersEachPieceAndCombines()
    {
        var policy = ShortReplacementSsnPolicy();
        policy.Config = new Config
        {
            Splitting = new Splitting { Enabled = true, Threshold = 20, Method = "newline" }
        };

        var input = "first 123-45-6789 line\nsecond 234-56-7890 line\nthird line";
        var result = new FilterService().Filter(policy, "context", 0, input);

        Assert.DoesNotContain("123-45-6789", result.FilteredText);
        Assert.DoesNotContain("234-56-7890", result.FilteredText);
        Assert.Contains("first # line", result.FilteredText);
        Assert.Contains("second # line", result.FilteredText);
        Assert.Contains("third line", result.FilteredText);

        // Two spans were found across the pieces, shifted to combined-document offsets.
        Assert.Equal(2, result.Spans.Count);
    }

    [Fact]
    public void SplittingBelowThresholdProcessesWholeDocument()
    {
        var policy = ShortReplacementSsnPolicy();
        policy.Config = new Config
        {
            Splitting = new Splitting { Enabled = true, Threshold = 10000, Method = "newline" }
        };

        var result = new FilterService().Filter(policy, "context", 0, "A 123-45-6789 B");

        Assert.Equal("A # B", result.FilteredText);
    }
}
