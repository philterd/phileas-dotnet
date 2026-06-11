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

using Phileas.Filters.Rules.Regex.RegexFilters;
using Phileas.Filters.Strategies.Rules;
using Phileas.Model;
using Xunit;
using static Phileas.Tests.IdentifierSectionTestSupport;

namespace Phileas.Tests;

public class SectionFilterTests
{
    private const string Start = "BEGIN-REDACT";
    private const string End = "END-REDACT";
    private const string Body = "BEGIN-REDACT This text should be redacted. END-REDACT";

    private static SectionFilter Filter() =>
        new(Config(new SectionFilterStrategy()), Start, End);

    [Fact]
    public void Section1()
    {
        var filtered = Filter().Filter(GetPolicy(), "context", Piece,
            "This is some test. BEGIN-REDACT This text should be redacted. END-REDACT This is outside the text.");
        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 19, 72, FilterType.Section));
        Assert.Equal(Body, filtered.Spans[0].Text);
    }

    [Fact]
    public void Section2_NoEndMarker()
    {
        var filtered = Filter().Filter(GetPolicy(), "context", Piece,
            "This is some test. BEGIN-REDACT This text should be redacted. This is outside the text.");
        Assert.Empty(filtered.Spans);
    }

    [Fact]
    public void Section3_AtStart()
    {
        var filtered = Filter().Filter(GetPolicy(), "context", Piece,
            "BEGIN-REDACT This text should be redacted. END-REDACT This is outside the text.");
        Assert.Single(filtered.Spans);
        Assert.True(CheckSpan(filtered.Spans[0], 0, 53, FilterType.Section));
        Assert.Equal(Body, filtered.Spans[0].Text);
    }
}
