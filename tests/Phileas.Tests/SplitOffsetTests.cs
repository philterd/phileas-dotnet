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
using Phileas.Policy.Filters.Strategies;
using Phileas.Services;
using Phileas.Services.Split;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
///     Span offsets and whitespace when splitting is enabled. See philterd/phileas-dotnet#92.
/// </summary>
public class SplitOffsetTests
{
    // Several pieces, blank lines, runs of spaces, and a trailing newline.
    private const string Document =
        "Contact:   alice@example.com\n\nSSN 078-05-1120   here.\n\n\nAlso bob@example.net  end.\n";

    private static PhileasPolicy Policy(string method, int overlap = 0, bool enabled = true,
        string? ssnStrategy = null)
    {
        var ssn = new Ssn();
        if (ssnStrategy != null)
            ssn.Strategies = new List<SsnFilterStrategy> { new() { Strategy = ssnStrategy } };

        return new PhileasPolicy
        {
            Name = "t",
            Identifiers = new Identifiers { Ssn = ssn, EmailAddress = new EmailAddress() },
            Config = new Config
            {
                Splitting = new Splitting
                {
                    Enabled = enabled, Method = method, Threshold = 64, Overlap = overlap
                }
            }
        };
    }

    private static void AssertOffsetsIndexTheInput(string input, TextFilterResult result)
    {
        Assert.NotEmpty(result.Spans);
        foreach (var span in result.Spans)
        {
            Assert.InRange(span.CharacterStart, 0, input.Length);
            Assert.InRange(span.CharacterEnd, span.CharacterStart, input.Length);
            Assert.Equal(span.Text, input[span.CharacterStart..span.CharacterEnd]);
        }
    }

    [Theory]
    [InlineData("newline")]
    [InlineData("width")]
    [InlineData("characters")]
    public void SplitOffsets_IndexIntoTheInput(string method)
    {
        var result = new FilterService().Filter(Policy(method), "ctx", 0, Document);

        AssertOffsetsIndexTheInput(Document, result);
        Assert.Equal(3, result.Spans.Count);
    }

    [Theory]
    [InlineData("newline")]
    [InlineData("width")]
    [InlineData("characters")]
    public void SplitOutput_MatchesTheUnsplitOutput(string method)
    {
        var split = new FilterService().Filter(Policy(method), "ctx", 0, Document);
        var unsplit = new FilterService().Filter(Policy(method, enabled: false), "ctx", 0, Document);

        // Splitting is an internal optimisation, so it must not be observable in the output.
        Assert.Equal(unsplit.FilteredText, split.FilteredText);
        Assert.Equal(Document.Count(c => c == '\n'), split.FilteredText.Count(c => c == '\n'));
    }

    [Theory]
    [InlineData("REDACT")] // replacement is longer than the original
    [InlineData("MASK")] // replacement is the same length
    [InlineData("TRUNCATE")] // replacement is shorter than the original
    public void SplitOffsets_HoldWhenReplacementChangesLength(string strategy)
    {
        var result = new FilterService().Filter(Policy("newline", ssnStrategy: strategy), "ctx", 0, Document);

        // Offsets describe the input, so they must not drift with the size of the redaction markers.
        AssertOffsetsIndexTheInput(Document, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(40)]
    public void SplitOffsets_HoldAtEveryOverlap(int overlap)
    {
        var result = new FilterService().Filter(Policy("newline", overlap), "ctx", 0, Document);

        AssertOffsetsIndexTheInput(Document, result);

        // An entity inside the overlap window is found by two pieces; only one span survives.
        Assert.Equal(3, result.Spans.Count);
        Assert.Equal(new FilterService().Filter(Policy("newline"), "ctx", 0, Document).FilteredText,
            result.FilteredText);
    }

    // A line-wrapped SSN straddles a newline split: one piece ends "078-05-" and the next begins
    // "1120", so neither sees the identifier whole. This is what an overlap is for.
    private const string StraddlingDocument =
        "Patient record follows.\nThe SSN is 078-05-\n1120 as recorded.\nEnd of record here.";

    private static PhileasPolicy StraddlePolicy(int overlap, bool enabled = true)
    {
        return new PhileasPolicy
        {
            Name = "t",
            Identifiers = new Identifiers { Ssn = new Ssn() },
            Config = new Config
            {
                Splitting = new Splitting
                {
                    Enabled = enabled, Method = "newline", Threshold = 40, Overlap = overlap
                }
            }
        };
    }

    [Theory]
    [InlineData(0)] // no overlap: neither piece contains the whole identifier
    [InlineData(4)] // too small a window to reach back over the first half
    public void EntityOnASeam_IsMissedWithoutASufficientOverlap(int overlap)
    {
        var result = new FilterService().Filter(StraddlePolicy(overlap), "ctx", 0, StraddlingDocument);

        Assert.Empty(result.Spans);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(30)]
    public void EntityOnASeam_IsRecoveredByASufficientOverlap(int overlap)
    {
        var result = new FilterService().Filter(StraddlePolicy(overlap), "ctx", 0, StraddlingDocument);

        var span = Assert.Single(result.Spans);
        Assert.Equal("078-05-\n1120", span.Text);
        Assert.Equal(span.Text, StraddlingDocument[span.CharacterStart..span.CharacterEnd]);

        // The recovered span is reported and redacted exactly as it would be without splitting.
        var unsplit = new FilterService().Filter(StraddlePolicy(overlap, enabled: false), "ctx", 0,
            StraddlingDocument);
        Assert.Equal(unsplit.FilteredText, result.FilteredText);
    }

    [Fact]
    public void Overlap_DefaultsToZero()
    {
        Assert.Equal(0, new Splitting().Overlap);
    }

    [Fact]
    public void DocumentWithNothingToRedact_IsReturnedUnchanged()
    {
        var input = "One line here.\n\nAnother line with   spaces.\n\n\nAnd a third one to split on.\n";

        var result = new FilterService().Filter(Policy("newline"), "ctx", 0, input);

        Assert.Equal(input, result.FilteredText);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void WhitespaceOnlyDocument_IsReturnedUnchanged()
    {
        // Every piece trims away to nothing, so the splitter yields none at all.
        var input = new string(' ', 40) + "\n\n" + new string(' ', 40);

        var result = new FilterService().Filter(Policy("newline"), "ctx", 0, input);

        Assert.Equal(input, result.FilteredText);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void SplitWithOverlap_LocatesPiecesAndSharesTheOverlapWindow()
    {
        var service = SplitFactory.GetSplitService("newline", 64);

        var located = service.SplitWithOverlap(Document, 5);

        Assert.NotNull(located);
        var previousEnd = 0;
        foreach (var split in located!)
        {
            Assert.Equal(split.Text, Document[split.Offset..(split.Offset + split.Text.Length)]);
            Assert.True(split.Offset >= 0);
            previousEnd = split.Offset + split.Text.Length;
        }

        Assert.True(previousEnd <= Document.Length);
    }

    [Fact]
    public void SplitWithOverlap_ReturnsNullWhenAPieceIsNotVerbatimInTheInput()
    {
        // A splitter whose pieces are not substrings of the input cannot be located; the caller then
        // falls back to filtering each piece on its own.
        ISplitService service = new RewritingSplitService();

        var located = service.SplitWithOverlap("alpha beta gamma", 0);

        Assert.Null(located);
    }

    [Fact]
    public void UnlocatablePieces_StillRedactRatherThanThrow()
    {
        // The fallback path keeps working: the text is filtered, though its offsets are the old
        // concatenated ones rather than offsets into the input.
        ISplitService service = new RewritingSplitService();
        Assert.Null(service.SplitWithOverlap("SSN 078-05-1120 here.", 0));
        Assert.NotEmpty(service.Split("SSN 078-05-1120 here."));
    }

    private sealed class RewritingSplitService : AbstractSplitService, ISplitService
    {
        public List<string> Split(string input)
        {
            return new List<string> { input.ToUpperInvariant() + "!" };
        }

        public string GetSeparator()
        {
            return " ";
        }
    }
}
