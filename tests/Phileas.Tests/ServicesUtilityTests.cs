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
using Phileas.Model.Metadata;
using Phileas.Services.Tokens;
using Phileas.Services.Validators;
using Xunit;

namespace Phileas.Tests;

public class WhitespaceTokenCounterTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("one", 1)]
    [InlineData("one two three", 3)]
    [InlineData("one   two\tthree\nfour", 4)]
    public void CountsTokens(string? text, long expected)
    {
        Assert.Equal(expected, new WhitespaceTokenCounter().CountTokens(text));
    }
}

public class DateSpanValidatorTests
{
    private static Span DateSpan(string text, string pattern)
    {
        var span = Span.Make(0, text.Length, FilterType.Date, "ctx", 0.9, text, "x", string.Empty, false, true,
            null, 0);
        span.Pattern = pattern;
        return span;
    }

    [Theory]
    [InlineData("2023-05-15", "uuuu-MM-dd", true)]
    [InlineData("05/15/2023", "MM/dd/uuuu", true)]
    [InlineData("2023-13-45", "uuuu-MM-dd", false)] // not a real date
    [InlineData("0500-01-01", "uuuu-MM-dd", false)] // year below 1800
    [InlineData("not-a-date", "uuuu-MM-dd", false)]
    public void ValidatesDates(string text, string pattern, bool expected)
    {
        Assert.Equal(expected, DateSpanValidator.GetInstance().Validate(DateSpan(text, pattern)));
    }
}

public class ZipCodeMetadataServiceTests
{
    [Fact]
    public void ReturnsPopulationForKnownZipCode()
    {
        var service = new ZipCodeMetadataService();

        // 90210 (Beverly Hills) is in the bundled census data.
        var (population, exists) = service.GetMetadata("90210");

        Assert.True(exists);
        Assert.True(population > 0);
    }

    [Fact]
    public void ReturnsNotFoundForUnknownZipCode()
    {
        var service = new ZipCodeMetadataService();

        var (population, exists) = service.GetMetadata("00000");

        Assert.False(exists);
        Assert.Equal(-1, population);
    }
}
