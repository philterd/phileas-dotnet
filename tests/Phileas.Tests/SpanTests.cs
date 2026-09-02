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
using Xunit;

namespace Phileas.Tests;

/// <summary>Mirrors the Java <c>SpanTest</c> (LAPPS/equals-contract cases excluded).</summary>
public class SpanTests
{
    private static Span S(int start, int end, FilterType type = FilterType.Age, double confidence = 1.0,
        int priority = 0, string text = "test")
    {
        return Span.Make(start, end, type, "context", confidence, text, "***", "salt", false, true,
            Array.Empty<string>(), priority);
    }

    [Fact]
    public void CopyProducesEqualFields()
    {
        var span1 = S(1, 6);
        var span2 = span1.Copy();

        Assert.Equal(span1.CharacterStart, span2.CharacterStart);
        Assert.Equal(span1.CharacterEnd, span2.CharacterEnd);
        Assert.Equal(span1.FilterType, span2.FilterType);
        Assert.Equal(span1.Confidence, span2.Confidence);
        Assert.Equal(span1.Text, span2.Text);
        Assert.Equal(span1.Replacement, span2.Replacement);
        Assert.Equal(span1.Salt, span2.Salt);
        Assert.Equal(span1.Priority, span2.Priority);
    }

    [Fact]
    public void ShiftSpans1()
    {
        var span1 = S(1, 6);
        var span2 = S(8, 12, priority: 2);
        var span3 = S(14, 20, priority: 5);
        var spans = new List<Span> { span1, span2, span3 };

        var shifted = Span.ShiftSpans(4, span1, spans);

        Assert.Equal(2, shifted.Count);
        Assert.Equal(12, shifted[0].CharacterStart);
        Assert.Equal(16, shifted[0].CharacterEnd);
        Assert.Equal(18, shifted[1].CharacterStart);
        Assert.Equal(24, shifted[1].CharacterEnd);
    }

    [Fact]
    public void ShiftSpans2()
    {
        var span1 = S(1, 6);
        var shifted = Span.ShiftSpans(4, span1, new List<Span> { span1 });
        Assert.Empty(shifted);
    }

    [Fact]
    public void DoesIndexStartSpan1()
    {
        var span1 = S(1, 6);
        var span2 = S(8, 12);
        var found = Span.DoesIndexStartSpan(8, new List<Span> { span1, span2 });
        Assert.NotNull(found);
        Assert.Same(span2, found);
    }

    [Fact]
    public void DoesIndexStartSpan2()
    {
        var span1 = S(1, 6);
        var span2 = S(8, 12);
        var found = Span.DoesIndexStartSpan(1, new List<Span> { span1, span2 });
        Assert.NotNull(found);
        Assert.Same(span1, found);
    }

    [Fact]
    public void DoesIndexStartSpan3()
    {
        var found = Span.DoesIndexStartSpan(4, new List<Span> { S(1, 6), S(8, 12) });
        Assert.Null(found);
    }

    [Fact]
    public void Overlapping1()
    {
        var result = Span.DropOverlappingSpans(new List<Span> { S(1, 5), S(2, 12) });
        Assert.Single(result);
        Assert.Equal(2, result[0].CharacterStart);
        Assert.Equal(12, result[0].CharacterEnd);
    }

    [Fact]
    public void Overlapping2()
    {
        var result = Span.DropOverlappingSpans(new List<Span> { S(2, 12, confidence: 0.5), S(2, 12) });
        Assert.Single(result);
        Assert.Equal(1.0, result[0].Confidence);
    }

    [Fact]
    public void Overlapping3()
    {
        var result = Span.DropOverlappingSpans(new List<Span> { S(2, 12, confidence: 0.5), S(14, 20) });
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Overlapping4()
    {
        Assert.Single(Span.DropOverlappingSpans(new List<Span> { S(2, 12, confidence: 0.5) }));
    }

    [Fact]
    public void Overlapping5()
    {
        var result = Span.DropOverlappingSpans(new List<Span> { S(7, 17, confidence: 0.5), S(0, 17) });
        Assert.Single(result);
        Assert.Equal(0, result[0].CharacterStart);
        Assert.Equal(17, result[0].CharacterEnd);
        Assert.Equal(1.0, result[0].Confidence);
    }

    [Fact]
    public void Overlapping6_EqualExtentHigherPriorityWins()
    {
        var result = Span.DropOverlappingSpans(new List<Span>
        {
            S(7, 17, FilterType.ZipCode, priority: 1),
            S(7, 17, FilterType.Identifier, priority: 2)
        });

        Assert.Single(result);
        Assert.Equal(FilterType.Identifier, result[0].FilterType);
    }

    [Fact]
    public void Overlapping7_LongestWins()
    {
        var result = Span.DropOverlappingSpans(new List<Span>
        {
            S(7, 17, FilterType.ZipCode),
            S(10, 17, FilterType.Identifier),
            S(13, 17, FilterType.Identifier)
        });

        Assert.Single(result);
        Assert.Equal(7, result[0].CharacterStart);
        Assert.Equal(FilterType.ZipCode, result[0].FilterType);
    }

    [Fact]
    public void Overlapping8()
    {
        var result = Span.DropOverlappingSpans(new List<Span>
        {
            S(10, 38, FilterType.Surname),
            S(20, 38, FilterType.Surname),
            S(24, 38, FilterType.Surname),
            S(29, 38, FilterType.Surname)
        });

        Assert.Single(result);
        Assert.Equal(10, result[0].CharacterStart);
        Assert.Equal(38, result[0].CharacterEnd);
    }

    [Fact]
    public void Overlapping9()
    {
        var result = Span.DropOverlappingSpans(new List<Span>
        {
            S(0, 6, FilterType.Surname),
            S(0, 12, FilterType.Surname),
            S(0, 18, FilterType.Surname)
        });

        Assert.Single(result);
        Assert.Equal(18, result[0].CharacterEnd);
    }

    [Fact]
    public void DropOverlappingSelectsCorrectWinnerAcrossMultipleGroups()
    {
        var spans = new List<Span>
        {
            // Group A: longest span wins.
            S(2, 7), S(0, 10), S(5, 9),
            // Group B: same extent, highest confidence wins.
            S(20, 24, confidence: 0.5), S(20, 24, confidence: 0.9),
            // Group C: same extent and confidence, highest priority wins.
            S(30, 40, FilterType.ZipCode, priority: 1), S(30, 40, FilterType.Identifier, priority: 5),
            // Disjoint span: kept untouched.
            S(50, 55)
        };

        var result = Span.DropOverlappingSpans(spans);

        Assert.Equal(4, result.Count);
        Assert.Equal(0, result[0].CharacterStart);
        Assert.Equal(10, result[0].CharacterEnd);
        Assert.Equal(20, result[1].CharacterStart);
        Assert.Equal(0.9, result[1].Confidence);
        Assert.Equal(30, result[2].CharacterStart);
        Assert.Equal(FilterType.Identifier, result[2].FilterType);
        Assert.Equal(50, result[3].CharacterStart);

        for (var i = 0; i < result.Count; i++)
        for (var j = i + 1; j < result.Count; j++)
        {
            var a = result[i];
            var b = result[j];
            Assert.False(a.CharacterStart <= b.CharacterEnd && b.CharacterStart <= a.CharacterEnd,
                "result must not contain overlapping spans");
        }
    }

    [Fact]
    public void DropOverlappingDoesNotMutateTheInputList()
    {
        var spans = new List<Span> { S(1, 5), S(2, 12), S(14, 20) };
        var snapshot = new List<Span>(spans);

        Span.DropOverlappingSpans(spans);

        Assert.Equal(snapshot, spans);
    }

    [Fact]
    public void Priority()
    {
        var result = Span.DropOverlappingSpans(new List<Span>
        {
            S(0, 5, FilterType.CreditCard, priority: 1, text: "Smith"),
            S(7, 11, FilterType.ZipCode, priority: 3, text: "John"),
            S(7, 11, FilterType.Age, priority: 5, text: "John")
        });

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.True(s.FilterType is FilterType.CreditCard or FilterType.Age));
    }

    [Fact]
    public void PriorityWithEqualPriorities()
    {
        var result = Span.DropOverlappingSpans(new List<Span>
        {
            S(0, 5, FilterType.CreditCard, priority: 5, text: "Smith"),
            S(7, 11, FilterType.ZipCode, priority: 7, text: "John"),
            S(7, 11, FilterType.Age, priority: 1, text: "John")
        });

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.True(s.FilterType is FilterType.CreditCard or FilterType.ZipCode));
    }

    [Fact]
    public void GetIdenticalSpans1()
    {
        var span1 = S(7, 17, FilterType.ZipCode);
        var spans = new List<Span>
        {
            span1,
            S(7, 17, FilterType.ZipCode),
            S(7, 17, FilterType.Identifier),
            S(4, 19, FilterType.Identifier),
            S(22, 25, FilterType.Identifier)
        };

        Assert.Single(Span.GetIdenticalSpans(span1, spans));
    }

    [Fact]
    public void GetIdenticalSpans2()
    {
        var span1 = S(7, 17, FilterType.ZipCode);
        var spans = new List<Span>
        {
            span1,
            S(7, 17, FilterType.Identifier),
            S(4, 19, FilterType.Identifier),
            S(22, 25, FilterType.Identifier),
            S(7, 17, FilterType.Url)
        };

        Assert.Equal(2, Span.GetIdenticalSpans(span1, spans).Count);
    }

    [Fact]
    public void GetIdenticalSpans3()
    {
        var span1 = S(7, 17, FilterType.ZipCode);
        var spans = new List<Span>
        {
            span1,
            S(7, 17, FilterType.ZipCode),
            S(7, 17, FilterType.Identifier),
            S(4, 19, FilterType.Identifier),
            S(22, 25, FilterType.Identifier),
            S(7, 17, FilterType.Url),
            S(22, 25, FilterType.Age)
        };

        Assert.Equal(2, Span.GetIdenticalSpans(span1, spans).Count);
    }

    [Theory]
    [InlineData(7, 11, 13, 17, "asdfbf test qwer asdf", true)] // separated by a space
    [InlineData(7, 11, 12, 16, "asdfbf testqwer asdf", true)] // directly adjacent
    [InlineData(7, 11, 15, 16, "asdfbf test   qwer asdf", true)] // whitespace run
    [InlineData(7, 11, 15, 16, "asdfbf test f  qwer asdf", false)] // word between
    [InlineData(7, 11, 15, 16, "asdfbf test .  qwer asdf", false)] // period between
    [InlineData(0, 5, 7, 11, "Smith, John D asdf", true)] // comma-separated
    public void AreSpansAdjacent(int start1, int end1, int start2, int end2, string text, bool expected)
    {
        Assert.Equal(expected, Span.AreSpansAdjacent(S(start1, end1), S(start2, end2), text));
    }

    [Fact]
    public void AreSpansAdjacent_WrongOrderIsFalse()
    {
        var span1 = S(7, 11);
        var span2 = S(15, 16);
        Assert.False(Span.AreSpansAdjacent(span2, span1, "asdfbf test    qwer asdf"));
    }
}
