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
using Phileas.Services;
using Xunit;

namespace Phileas.Tests;

/// <summary>
///     The written date forms the date filter detects, and what the date strategies do with each.
///     Pins the whole table so a form that regresses is caught. See philterd/phileas-dotnet#111.
/// </summary>
public class DateFormatCoverageTests
{
    private static string Detected(string date, bool onlyValidDates = false)
    {
        var json = "{\"identifiers\":{\"date\":{\"onlyValidDates\":" + (onlyValidDates ? "true" : "false") + "}}}";
        var spans = new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, "on " + date + " ok").Spans;

        return spans.Count == 0 ? string.Empty : string.Join("|", spans.Select(s => s.Text));
    }

    private static string Strategy(string date, string strategy, string fields = "")
    {
        var json = "{\"identifiers\":{\"date\":{\"dateFilterStrategies\":[{\"strategy\":\"" + strategy + "\""
                   + fields + "}]}}}";
        return new FilterService().Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, date)
            .FilteredText;
    }

    [Theory]
    // Detected before #111.
    [InlineData("January 15, 1990")]
    [InlineData("Jan 15, 1990")]
    [InlineData("Jan. 15, 1990")]
    [InlineData("January 15 1990")]
    [InlineData("15 January 1990")]
    [InlineData("15.01.1990")]
    // Added by #111.
    [InlineData("1990-01-15")]
    [InlineData("1990/01/15")]
    [InlineData("1990.01.15")]
    [InlineData("15 Jan 1990")]
    [InlineData("15 Jan. 1990")]
    [InlineData("15-Jan-1990")]
    [InlineData("15/Jan/1990")]
    [InlineData("15-January-1990")]
    [InlineData("05-Jan-1990")]
    public void EachWrittenFormIsDetectedWhole(string date)
    {
        // Whole, not merely overlapping: a year-first date read as a day-first one would produce a
        // span over part of the text rather than all of it.
        Assert.Equal(date, Detected(date));
    }

    [Theory]
    [InlineData("1990")] // a year alone
    [InlineData("$1,990.00")] // an amount
    [InlineData("2020.1.5")] // a version string: year-first requires a zero-padded month and day
    [InlineData("1990-2-3")] // the same, with the ISO delimiter
    [InlineData("2020-555-1234")] // a phone number
    [InlineData("1234-5678-9012-3456")] // a payment card
    [InlineData("45 January 1990")] // a day out of range
    [InlineData("15-January 1990")] // mismatched separators
    public void SomethingThatIsNotADateIsNotDetected(string text)
    {
        Assert.Equal(string.Empty, Detected(text));
    }

    [Fact]
    public void OnlyValidDatesStillRejectsAnImpossibleYearFirstDate()
    {
        Assert.Equal("1990-02-31", Detected("1990-02-31"));
        Assert.Equal(string.Empty, Detected("1990-02-31", onlyValidDates: true));
    }

    [Fact]
    public void OnlyValidDatesKeepsARealYearFirstDate()
    {
        Assert.Equal("1990-01-15", Detected("1990-01-15", onlyValidDates: true));
    }

    [Theory]
    [InlineData("1990-01-15", "1990-02-14")]
    [InlineData("1990/01/15", "1990/02/14")]
    [InlineData("1990.01.15", "1990.02.14")]
    [InlineData("15 Jan 1990", "14 Feb 1990")]
    [InlineData("15 Jan. 1990", "14 Feb. 1990")]
    [InlineData("15-Jan-1990", "14-Feb-1990")]
    [InlineData("15/Jan/1990", "14/Feb/1990")]
    [InlineData("15-January-1990", "14-February-1990")]
    [InlineData("05-Jan-1990", "04-Feb-1990")] // a zero-padded day stays zero-padded
    public void ShiftPreservesTheFormOfANewlyDetectedDate(string date, string shifted)
    {
        Assert.Equal(shifted, Strategy(date, "SHIFT", ",\"shiftDays\": 30"));
    }

    [Theory]
    [InlineData("1990-01-15")]
    [InlineData("1990/01/15")]
    [InlineData("1990.01.15")]
    [InlineData("15 Jan 1990")]
    [InlineData("15-Jan-1990")]
    [InlineData("15/Jan/1990")]
    [InlineData("15-January-1990")]
    public void TruncateToYearActsOnANewlyDetectedDate(string date)
    {
        Assert.Equal("1990", Strategy(date, "TRUNCATE_TO_YEAR"));
    }

    [Theory]
    [InlineData("1990-01-15")]
    [InlineData("15-Jan-1990")]
    [InlineData("15/Jan/1990")]
    public void RelativeActsOnANewlyDetectedDate(string date)
    {
        var replaced = Strategy(date, "RELATIVE");

        Assert.EndsWith(" ago", replaced);
        Assert.DoesNotContain("1990", replaced);
    }

    [Theory]
    [InlineData("1990-01-15T10:30:00", "1990-01-15")]
    [InlineData("2024-06-01T09:30:00Z", "2024-06-01")]
    [InlineData("1990-01-15t10:30:00", "1990-01-15")]
    [InlineData("1990-01-15T10:30:00.123Z", "1990-01-15")]
    [InlineData("1990-01-15 10:30:00", "1990-01-15")]
    public void TheDatePartOfAnIso8601TimestampIsDetected(string timestamp, string date)
    {
        // A log line or an export writes the timestamp far more often than the bare date, and the
        // date is the part that is PHI. A word boundary alone would reject the whole timestamp,
        // since the T that follows the date is a word character.
        Assert.Equal(date, Detected(timestamp));
        Assert.Equal(date, Detected(timestamp, onlyValidDates: true));
    }

    [Fact]
    public void ShiftLeavesTheTimeOfATimestampAlone()
    {
        Assert.Equal("2024-07-01T09:30:00Z", Strategy("2024-06-01T09:30:00Z", "SHIFT", ",\"shiftDays\": 30"));
    }

    [Theory]
    [InlineData("1990-01-15x")] // the T guard is for a time, not for any letter
    [InlineData("x1990-01-15")]
    [InlineData("1990-01-15_")]
    [InlineData("15-Jan-1990x")]
    public void ADateRunTogetherWithAWordIsNotDetected(string text)
    {
        Assert.Equal(string.Empty, Detected(text));
    }

    [Theory]
    [InlineData("1990/02/31")]
    [InlineData("1990.02.31")]
    [InlineData("1990-04-31")] // April has thirty days
    [InlineData("1900-02-29")] // 1900 is not a leap year
    public void OnlyValidDatesRejectsAnImpossibleDateOnEveryYearFirstDelimiter(string date)
    {
        Assert.Equal(date, Detected(date));
        Assert.Equal(string.Empty, Detected(date, onlyValidDates: true));
    }

    [Fact]
    public void OnlyValidDatesKeepsALeapDay()
    {
        Assert.Equal("2000-02-29", Detected("2000-02-29", onlyValidDates: true));
    }

    [Theory]
    [InlineData("{0}")] // the whole document
    [InlineData("{0} tail")] // at the start
    [InlineData("head {0}")] // at the end
    [InlineData("({0})")]
    [InlineData("\"{0}\"")]
    [InlineData("DOB:{0}")] // no space before it
    [InlineData("{0},")]
    [InlineData("\n{0}\n")]
    public void ADateIsFoundWhereverItSitsAndItsOffsetsIndexTheInput(string template)
    {
        // Every other test here puts the date between two words, so a pattern that only worked with
        // whitespace around it would pass. The offsets are checked against the input rather than
        // against the span's own text, which is how #92 went unnoticed.
        foreach (var date in new[] { "1990-01-15", "15-Jan-1990", "15 Jan 1990", "15-January-1990" })
        {
            var input = string.Format(template, date);
            var span = Assert.Single(
                new FilterService().Filter(PolicySerializer.DeserializeFromJson("{\"identifiers\":{\"date\":{}}}"),
                    "ctx", 0, input).Spans);

            Assert.Equal(date, span.Text);
            Assert.Equal(date, input.Substring(span.CharacterStart, span.CharacterEnd - span.CharacterStart));
        }
    }

    [Fact]
    public void ARunOfYearFirstDatesIsDetectedAsTwoDates()
    {
        // A date range written without spaces. Each date is its own span rather than one running
        // across the pair, which is what the Java filter produces too.
        Assert.Equal("2024-06-01|2024-06-30", Detected("2024-06-01-2024-06-30"));
    }
}
