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
    [InlineData("She died at the age of 39", "age of 39")]
    [InlineData("He left at the age of 20", "age of 20")]
    [InlineData("Age of 39", "Age of 39")]
    [InlineData("at the age of 39.5", "age of 39.5")]
    public void Filter_DetectsAgeOf(string input, string spanText)
    {
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, input);
        Assert.Equal(spanText, Assert.Single(result.Spans).Text);
    }

    [Theory]
    [InlineData("They were between the ages of 39 and 41.", "ages of 39 and 41")]
    [InlineData("the ages of 39, 41, and 44", "ages of 39, 41, and 44")]
    [InlineData("Patients ages 39 to 41 enrolled.", "ages 39 to 41")]
    [InlineData("ages 39-41", "ages 39-41")]
    [InlineData("Ages: 39, 41", "Ages: 39, 41")]
    [InlineData("They were aged between 39 and 41", "aged between 39 and 41")]
    [InlineData("The sisters, aged 39 and 41, left.", "aged 39 and 41")]
    [InlineData("The brothers were aged 39 to 41", "aged 39 to 41")]
    [InlineData("The brothers were aged from 39 to 41", "aged from 39 to 41")]
    [InlineData("They were ages between 39 and 41", "ages between 39 and 41")]
    [InlineData("Eligible at age between 18 and 65.", "age between 18 and 65")]
    [InlineData("The sisters, aged 39-41, left.", "aged 39-41")]
    [InlineData("ages 39 - 41", "ages 39 - 41")]
    [InlineData("ages 39, 41 and 44", "ages 39, 41 and 44")]
    [InlineData("ages 39 and 41 and 44", "ages 39 and 41 and 44")]
    [InlineData("ages 39 and 41, and 44", "ages 39 and 41, and 44")]
    [InlineData("ages 5 or 6", "ages 5 or 6")]
    [InlineData("The siblings, aged 3, 5 and 7, left.", "aged 3, 5 and 7")]
    [InlineData("aged 39 or 41", "aged 39 or 41")]
    [InlineData("aged 39 and 41 and 44", "aged 39 and 41 and 44")]
    [InlineData("aged 39-41 and 44", "aged 39-41 and 44")]
    [InlineData("Age: between 18 and 65", "Age: between 18 and 65")]
    [InlineData("Ages - from 18 to 65", "Ages - from 18 to 65")]
    [InlineData("the ages of between 18 and 65", "ages of between 18 and 65")]
    [InlineData("Eligible age 18-65.", "age 18-65")]
    [InlineData("at age 39 to 41", "age 39 to 41")]
    [InlineData("Ages: 39, 2", "Ages: 39, 2")] // after the plural, a bare comma joins
    [InlineData("ages 3-5-7", "ages 3-5-7")] // a hyphen chain of plausible ages after the plural
    public void Filter_DetectsEveryAgeInARangeOrList(string input, string spanText)
    {
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, input);
        Assert.Equal(spanText, Assert.Single(result.Spans).Text);
    }

    [Theory]
    [InlineData("age 25-03-2024")]
    [InlineData("ages 12-25-2020")]
    [InlineData("Age: 12/25/2020")]
    [InlineData("aged 12-25-2020")]
    [InlineData("age between 12-25-2020")]
    public void ADateAfterAKeywordIsNotCutIntoAPartialAge(string input)
    {
        // A keyword match stopped at a hyphen or slash, so the first part of a date was redacted as
        // an age and the rest was left for the date filter to miss.
        Assert.Empty(CreateFilter().Filter(CreatePolicy(), "test", 0, input).Spans);
    }

    [Theory]
    [InlineData("Patient age 45, 3 visits", "age 45")]
    [InlineData("Age: 5, 10, 15 mg", "Age: 5")]
    [InlineData("age of 39, 2 children", "age of 39")]
    [InlineData("age 39 and 2 children", "age 39")]
    [InlineData("aged 65 and over", "aged 65")]
    [InlineData("aged 39, 2 children", "aged 39")]
    [InlineData("ages 20,,,21", "ages 20")]
    public void ANumberThatDoesNotContinueTheAgesIsNotRedacted(string input, string spanText)
    {
        // After "age" or "aged", a bare comma usually starts unrelated text, so it ends the match; a
        // malformed separator ends it after any keyword. Reading past either redacted the number that
        // followed.
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, input);
        Assert.Equal(spanText, Assert.Single(result.Spans).Text);
    }

    [Fact]
    public void AnAgeWrittenWithBothAKeywordAndAUnitIsOneSpan()
    {
        // "age of 39" and "39 years" overlap; the longer span is kept, which covers the value.
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, "She retired at the age of 39 years.");
        Assert.Equal("age of 39", Assert.Single(result.Spans).Text);
    }

    [Theory]
    [InlineData("No age mentioned here.")]
    [InlineData("The year is 2024.")]
    [InlineData("average of 39")]
    [InlineData("coverage of 39")]
    [InlineData("the age of steam")]
    [InlineData("images of 39")]
    [InlineData("agebetween 39")]
    [InlineData("agesd 39")]
    [InlineData("ages 20andand21")]
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

    [Theory]
    [InlineData("age", " ")]
    [InlineData("ages 1", ", ")]
    [InlineData("ages 1", " , and")]
    [InlineData("ages 1", ", 1")]
    [InlineData("aged 1", ", 1")]
    [InlineData("aged 1", " and 1, 1")]
    [InlineData("Age: between 1", ", 1")]
    public void AKeywordFollowedByARepeatedRunDoesNotBacktrackQuadratically(string keyword, string unit)
    {
        // The separator keeps its whitespace inside the optional group so a run of it has only one
        // possible match. Written the ambiguous way, \s*[:=-]?\s*, a whitespace run takes time
        // quadratic in its length. The comma list after "ages" is checked the same way.
        var filter = CreateFilter();
        var policy = CreatePolicy();
        string Input(int n) => keyword + string.Concat(Enumerable.Repeat(unit, n)) + "x";

        var shortRun = Time(() => filter.Filter(policy, "test", 0, Input(4_000)));
        var longRun = Time(() => filter.Filter(policy, "test", 0, Input(16_000)));

        // Four times the input; a quadratic pattern would be about sixteen times the work. The bar is
        // loose enough not to be flaky on a shared runner and still fails a quadratic regression.
        Assert.True(longRun < Math.Max(shortRun * 8, TimeSpan.FromMilliseconds(250).TotalMilliseconds),
            $"4x the input took {longRun:F1} ms against {shortRun:F1} ms, which looks quadratic");
    }

    // ---------------- spelled-out ages (#191) ----------------

    [Theory]
    [InlineData("The patient is thirty-five years old.", "thirty-five years old")]
    [InlineData("He is thirty five years old.", "thirty five years old")]
    [InlineData("She is a thirty-five-year-old female.", "thirty-five-year-old")]
    [InlineData("He is five yo.", "five yo")]
    [InlineData("The child was twelve yrs.", "twelve yrs")]
    [InlineData("Patient is twelve yrs old", "twelve yrs old")]
    [InlineData("He is one hundred years old.", "one hundred years old")]
    [InlineData("She is a hundred years old.", "a hundred years old")]
    [InlineData("one hundred and two years old", "one hundred and two years old")]
    [InlineData("one hundred and twenty-five years old", "one hundred and twenty-five years old")]
    [InlineData("She is twenty-two y/o.", "twenty-two y/o")]
    [InlineData("He is twenty two y/o.", "twenty two y/o")]
    [InlineData("a seventy-year-old patient", "seventy-year-old")]
    [InlineData("forty-one-yr-old", "forty-one-yr-old")]
    [InlineData("two yo", "two yo")]
    [InlineData("one hundred thirty years old", "one hundred thirty years old")]
    public void Filter_DetectsSpelledOutAgeWithUnits(string input, string spanText)
    {
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, input);
        Assert.Equal(spanText, Assert.Single(result.Spans).Text);
    }

    [Theory]
    [InlineData("She died at the age of thirty-nine", "age of thirty-nine")]
    [InlineData("He left at the age of twenty", "age of twenty")]
    [InlineData("at the age of thirty-nine", "age of thirty-nine")]
    [InlineData("Age of thirty-nine", "Age of thirty-nine")]
    [InlineData("age thirty-nine", "age thirty-nine")]
    [InlineData("aged forty one", "aged forty one")]
    [InlineData("aged forty-two", "aged forty-two")]
    [InlineData("Age: twenty", "Age: twenty")]
    [InlineData("Age:twenty", "Age:twenty")]
    [InlineData("Age = twenty", "Age = twenty")]
    [InlineData("Age - twenty", "Age - twenty")]
    [InlineData("age of one hundred and two", "age of one hundred and two")]
    [InlineData("age of one hundred two", "age of one hundred two")]
    [InlineData("age of one hundred", "age of one hundred")]
    [InlineData("age of a hundred", "age of a hundred")]
    [InlineData("age of one hundred and twenty-five", "age of one hundred and twenty-five")]
    [InlineData("age of one hundred twenty-five", "age of one hundred twenty-five")]
    [InlineData("age of zero", "age of zero")]
    [InlineData("age zero", "age zero")]
    public void Filter_DetectsSpelledOutAgeAfterKeyword(string input, string spanText)
    {
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, input);
        Assert.Equal(spanText, Assert.Single(result.Spans).Text);
    }

    [Theory]
    [InlineData("THIRTY-FIVE YEARS OLD", "THIRTY-FIVE YEARS OLD")]
    [InlineData("Age Of Thirty-Nine", "Age Of Thirty-Nine")]
    [InlineData("AGED FORTY ONE", "AGED FORTY ONE")]
    [InlineData("Age: Twenty", "Age: Twenty")]
    [InlineData("ONE HUNDRED AND TWO YEARS OLD", "ONE HUNDRED AND TWO YEARS OLD")]
    public void SpelledOutAge_CaseInsensitivity(string input, string spanText)
    {
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, input);
        Assert.Equal(spanText, Assert.Single(result.Spans).Text);
    }

    [Theory]
    [InlineData("age of one hundred twenty-five", true)]
    [InlineData("age of one hundred and twenty-five", true)]
    [InlineData("age of one hundred and twenty", true)]
    [InlineData("age of one hundred and nineteen", true)]
    [InlineData("age of one hundred", true)]
    [InlineData("age of a hundred", true)]
    [InlineData("age of one hundred and twenty-six", false)]
    [InlineData("age of one hundred twenty-six", false)]
    [InlineData("age of one hundred thirty", false)]
    [InlineData("age of one hundred and thirty", false)]
    [InlineData("age of one hundred thirty-five", false)]
    [InlineData("age of one hundred forty", false)]
    [InlineData("age of one hundred ninety-nine", false)]
    [InlineData("age of two hundred", false)]
    [InlineData("age of two hundred and one", false)]
    [InlineData("age of three hundred", false)]
    [InlineData("age two hundred", false)]
    [InlineData("aged one hundred twenty-six", false)]
    public void SpelledOutAge_KeywordCeilingAt125(string input, bool detected)
    {
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, input);
        Assert.Equal(detected, result.Spans.Count > 0);
    }

    [Theory]
    [InlineData("age often")]
    [InlineData("aged tenderloin")]
    [InlineData("aged cheddar")]
    [InlineData("storage of thirty")]
    [InlineData("average of twenty")]
    [InlineData("coverage of thirty")]
    [InlineData("images of thirty")]
    [InlineData("agethirty")]
    [InlineData("ageofthirty")]
    [InlineData("storage - twenty")]
    [InlineData("mileage: twenty")]
    public void SpelledOutAge_FalsePositivesAreRejected(string input)
    {
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, input);
        Assert.Empty(result.Spans);
    }

    [Fact]
    public void AnAgeWrittenWithBothASpelledKeywordAndAUnitIsOneSpan()
    {
        var result = CreateFilter().Filter(CreatePolicy(), "test", 0, "She retired at the age of thirty-nine years.");
        Assert.Equal("age of thirty-nine", Assert.Single(result.Spans).Text);
    }

    [Fact]
    public void FilterService_RedactsSpelledOutAge()
    {
        var policy = new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Age = new Age() }
        };
        var result = new FilterService().Filter(policy, "test", 0, "The patient is thirty-five years old.");
        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("thirty-five years old", result.FilteredText);
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