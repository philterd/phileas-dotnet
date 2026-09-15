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

using Phileas.Filters;
using Phileas.Filters.Rules.Regex.RegexFilters;
using Phileas.Filters.Strategies.Rules;
using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

public class AgeFilterTests
{
    private static AgeFilter CreateFilter()
    {
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new AgeFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .Build();
        return new AgeFilter(config);
    }

    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Age = new Age() }
        };
    }

    [Theory]
    [InlineData("The patient is 45 years old.")]
    [InlineData("He is 30 yo.")]
    [InlineData("age 25")]
    [InlineData("aged 65")]
    [InlineData("She is 22 y/o.")]
    public void Filter_DetectsAge(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.NotEmpty(result.Spans);
    }

    [Fact]
    public void Filter_ReplacesAgeWithRedaction()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, "The patient is 45 years old.");
        Assert.All(result.Spans, span => Assert.Contains("REDACTED", span.Replacement));
    }

    [Theory]
    [InlineData("No age mentioned here.")]
    [InlineData("The year is 2024.")]
    public void Filter_DoesNotDetectNonAge(string input)
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, input);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void Filter_EmptyInput_ReturnsNoSpans()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, string.Empty);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void Filter_ReturnsCorrectFilterType()
    {
        var filter = CreateFilter();
        var policy = CreatePolicy();
        var result = filter.Filter(policy, "test", 0, "The patient is 45 years old.");
        Assert.NotEmpty(result.Spans);
        Assert.Equal(FilterType.Age, result.Spans[0].FilterType);
    }

    // ---------------- the keyword separator (#68) ----------------

    [Theory]
    [InlineData("Age: 47")]
    [InlineData("Age:47")]
    [InlineData("Age : 47")]
    [InlineData("Age = 47")]
    [InlineData("Age=47")]
    [InlineData("Age - 47")]
    [InlineData("Age-47")]
    [InlineData("Age 47")]
    [InlineData("AGE:47")]
    [InlineData("aged: 39")]
    [InlineData("aged=39.5")]
    public void AKeywordSeparatorIsAccepted(string input)
    {
        // "Age: 47" is how age is written in a structured clinical or intake record, and the pattern
        // allowed only whitespace, so the most common written form went undetected.
        Assert.NotEmpty(CreateFilter().Filter(CreatePolicy(), "test", 0, input).Spans);
    }

    [Theory]
    [InlineData("Age:\n47")]
    [InlineData("Age\n47")]
    [InlineData("Age:\r\n47")]
    public void ALabelOnOneLineAndItsValueOnTheNextIsDetected(string input)
    {
        Assert.NotEmpty(CreateFilter().Filter(CreatePolicy(), "test", 0, input).Spans);
    }

    [Theory]
    [InlineData("coverage: 47")]
    [InlineData("mileage: 45000")]
    [InlineData("average: 47")]
    [InlineData("storage - 12")]
    [InlineData("Portage=30")]
    public void AWordEndingInAgeIsNotAKeyword(string input)
    {
        Assert.Empty(CreateFilter().Filter(CreatePolicy(), "test", 0, input).Spans);
    }

    // ---------------- the plausibility bound (#69) ----------------

    [Theory]
    [InlineData("form AGE 2024")]
    [InlineData("Bronze Age 1200")]
    [InlineData("Stone Age-2000")]
    [InlineData("Age: 2024")]
    [InlineData("age 126")]
    [InlineData("age 130")]
    [InlineData("age 999")]
    public void AnImplausibleNumberAfterAKeywordIsNotAnAge(string input)
    {
        // The keyword pattern kept any number that followed it, so a historical period or a form
        // field number was redacted as an age.
        Assert.Empty(CreateFilter().Filter(CreatePolicy(), "test", 0, input).Spans);
    }

    [Theory]
    [InlineData("age 0", true)]
    [InlineData("age 1", true)]
    [InlineData("age 99", true)]
    [InlineData("age 100", true)]
    [InlineData("age 119", true)]
    [InlineData("age 120", true)]
    [InlineData("age 125", true)] // the ceiling is inclusive
    [InlineData("age 126", false)]
    [InlineData("age 125.9", true)]
    [InlineData("age 39.5", true)] // a decimal age still reads
    public void TheBoundIsWhereItIsDocumented(string input, bool detected)
    {
        // Set generously rather than at a typical maximum lifespan: the cost of the bound is recall on
        // a genuine but extreme age.
        Assert.Equal(detected, CreateFilter().Filter(CreatePolicy(), "test", 0, input).Spans.Count > 0);
    }

    [Theory]
    [InlineData("AGE 047")] // a fixed-width form export
    [InlineData("age 007")]
    [InlineData("age 09")]
    [InlineData("Age: 002")]
    [InlineData("age 0125")]
    public void AZeroPaddedValueIsStillAnAge(string input)
    {
        // A bound written without room for leading zeros would have dropped the padded value a
        // fixed-width record carries, which detected before the bound existed.
        Assert.NotEmpty(CreateFilter().Filter(CreatePolicy(), "test", 0, input).Spans);
    }

    [Theory]
    [InlineData("age 0126")] // padding does not widen the bound
    [InlineData("age 02024")]
    [InlineData("age 0000")]
    public void PaddingDoesNotSmuggleAnImplausibleValuePastTheBound(string input)
    {
        Assert.Empty(CreateFilter().Filter(CreatePolicy(), "test", 0, input).Spans);
    }

    [Theory]
    [InlineData("1200 years old")]
    [InlineData("2024 years old")]
    [InlineData("130 yrs")]
    [InlineData("47-year-old")]
    [InlineData("3.5 years old")]
    public void TheBoundDoesNotReachTheFormsThatCarryTheirOwnUnit(string input)
    {
        // "years old" and "y/o" are what make those forms ages, so a number in front of one needs no
        // plausibility check. The bound applies to the keyword pattern alone.
        Assert.NotEmpty(CreateFilter().Filter(CreatePolicy(), "test", 0, input).Spans);
    }

    [Theory]
    [InlineData("2026-01-15")]
    [InlineData("01/15/1990")]
    [InlineData("15-Jan-1990")]
    public void ADateIsStillNotAnAge(string input)
    {
        Assert.Empty(CreateFilter().Filter(CreatePolicy(), "test", 0, input).Spans);
    }

    [Fact]
    public void AKeywordFollowedByAWhitespaceRunDoesNotBacktrackQuadratically()
    {
        // The separator keeps its whitespace inside the optional group so a run of it has only one
        // possible match. Written the ambiguous way, \s*[:=-]?\s*, this input takes time quadratic in
        // the length of the run. Doubling the run must not quadruple the work.
        var filter = CreateFilter();
        var policy = CreatePolicy();

        var shortRun = Time(() => filter.Filter(policy, "test", 0, "age" + new string(' ', 4_000) + "x"));
        var longRun = Time(() => filter.Filter(policy, "test", 0, "age" + new string(' ', 16_000) + "x"));

        // Four times the input; a quadratic pattern would be about sixteen times the work. The bar is
        // loose enough not to be flaky on a shared runner and still fails a quadratic regression.
        Assert.True(longRun < Math.Max(shortRun * 8, TimeSpan.FromMilliseconds(250).TotalMilliseconds),
            $"4x the input took {longRun:F1} ms against {shortRun:F1} ms, which looks quadratic");
    }

    private static double Time(Action action)
    {
        action(); // let the compiled patterns warm up
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 5; i++) action();
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds / 5;
    }

    [Fact]
    public void FilterService_RedactsAge()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Age = new Age() }
        };
        var result = new FilterService().Filter(policy, "test", 0, "The patient is 45 years old.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("45 years old", result.FilteredText);
    }
}