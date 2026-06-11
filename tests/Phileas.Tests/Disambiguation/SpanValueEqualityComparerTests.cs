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
using Phileas.Services.Disambiguation;
using Xunit;

namespace Phileas.Tests.Disambiguation;

public class SpanValueEqualityComparerTests
{
    private static readonly SpanValueEqualityComparer Comparer = SpanValueEqualityComparer.Instance;

    private static Span Make(FilterType filterType = FilterType.Ssn, string text = "123-45-6789",
        int start = 0, int end = 11, string[]? window = null)
    {
        return Span.Make(start, end, filterType, "ctx", 0.5, text, "REDACTED", "", false, true,
            window ?? new[] { "a", "b" }, 0);
    }

    [Fact]
    public void IdenticalValueFields_AreEqualWithSameHash()
    {
        var a = Make();
        var b = Make();

        Assert.True(Comparer.Equals(a, b));
        Assert.Equal(Comparer.GetHashCode(a), Comparer.GetHashCode(b));
    }

    [Fact]
    public void WindowIsExcludedFromEquality()
    {
        // The context window is a transient field and must not affect value equality.
        var a = Make(window: new[] { "phone", "number" });
        var b = Make(window: new[] { "social", "security" });

        Assert.True(Comparer.Equals(a, b));
        Assert.Equal(Comparer.GetHashCode(a), Comparer.GetHashCode(b));
    }

    [Theory]
    [InlineData(FilterType.PhoneNumber, "123-45-6789", 0, 11)]
    [InlineData(FilterType.Ssn, "999-99-9999", 0, 11)]
    [InlineData(FilterType.Ssn, "123-45-6789", 5, 11)]
    [InlineData(FilterType.Ssn, "123-45-6789", 0, 9)]
    public void DifferingFields_AreNotEqual(FilterType filterType, string text, int start, int end)
    {
        var a = Make();
        var b = Make(filterType, text, start, end);

        Assert.False(Comparer.Equals(a, b));
    }

    [Fact]
    public void NullHandling()
    {
        var span = Make();
        Assert.True(Comparer.Equals(null, null));
        Assert.False(Comparer.Equals(span, null));
        Assert.False(Comparer.Equals(null, span));
        Assert.True(Comparer.Equals(span, span));
    }
}
