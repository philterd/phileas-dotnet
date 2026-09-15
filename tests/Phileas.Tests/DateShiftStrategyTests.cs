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

using System.Text.Json;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
///     The date SHIFT strategy's policy fields. See philterd/phileas-dotnet#80.
/// </summary>
public class DateShiftStrategyTests
{
    private const string Text = "seen on 01/15/1990 today";

    private static string Filter(string json)
    {
        return new FilterService().Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, Text)
            .FilteredText;
    }

    private static string PolicyJson(string strategy, string fields)
    {
        return "{\"identifiers\":{\"date\":{\"dateFilterStrategies\":[{\"strategy\":\"" + strategy + "\","
               + fields + "}]}}}";
    }

    [Theory]
    [InlineData("SHIFT")] // what the schema and the PhiSQL compiler emit
    [InlineData("SHIFT_DATE")] // the name this port has always used
    public void ShiftDays_FromACompiledPolicy_ActuallyShifts(string strategy)
    {
        // Before this was bound, a compiled SHIFT policy redacted the date instead of shifting it.
        Assert.Equal("seen on 2/14/1990 today", Filter(PolicyJson(strategy, "\"shiftDays\": 30")));
    }

    [Fact]
    public void ShiftMonthsAndYears_AreBound()
    {
        Assert.Equal("seen on 3/15/1990 today", Filter(PolicyJson("SHIFT", "\"shiftMonths\": 2")));
        Assert.Equal("seen on 1/15/1995 today", Filter(PolicyJson("SHIFT", "\"shiftYears\": 5")));
    }

    [Fact]
    public void TheSchemaFieldNames_AreWhatTheModelBinds()
    {
        var strategy = JsonSerializer.Deserialize<DateFilterStrategy>(
            "{\"shiftDays\":1,\"shiftMonths\":2,\"shiftYears\":3,\"shiftRandom\":true,\"futureDates\":true}")!;

        Assert.Equal(1, strategy.ShiftDays);
        Assert.Equal(2, strategy.ShiftMonths);
        Assert.Equal(3, strategy.ShiftYears);
        Assert.True(strategy.ShiftRandom);
        Assert.True(strategy.FutureDates);
    }

    [Fact]
    public void Defaults_MatchTheSchema()
    {
        var strategy = new DateFilterStrategy();

        Assert.Equal(0, strategy.ShiftDays);
        Assert.Equal(0, strategy.ShiftMonths);
        Assert.Equal(0, strategy.ShiftYears);
        Assert.False(strategy.ShiftRandom);
        Assert.False(strategy.FutureDates);
    }

    [Fact]
    public void ShiftRandom_MovesTheDateAndIgnoresTheConfiguredOffsets()
    {
        // The random range is one to twenty-nine days forward, one to eleven months forward, and one
        // or two years back, so the result is always a different date and never the configured one.
        var results = new HashSet<string>();
        for (var i = 0; i < 40; i++)
            results.Add(Filter(PolicyJson("SHIFT", "\"shiftDays\": 30, \"shiftRandom\": true")));

        Assert.DoesNotContain("seen on 01/15/1990 today", results);
        Assert.DoesNotContain("seen on 2/14/1990 today", results); // not the configured shift
        Assert.True(results.Count > 1, "a random shift should not produce one value every time");
    }

    [Fact]
    public void FutureDates_False_KeepsAPastDateInThePast()
    {
        // A large forward shift on a recent date would land ahead of today.
        var recent = DateTime.Today.AddDays(-5);
        var input = "seen on " + recent.ToString("M/d/yyyy") + " today";
        var json = PolicyJson("SHIFT", "\"shiftDays\": 100");

        var filtered = new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, input).FilteredText;

        var shifted = DateTime.Parse(filtered.Replace("seen on ", "").Replace(" today", ""));
        Assert.True(shifted <= DateTime.Today, $"{shifted:d} should not be in the future");
        Assert.Equal(recent.AddDays(-100).Date, shifted.Date);
    }

    [Fact]
    public void FutureDates_True_AllowsTheShiftToLandAhead()
    {
        var recent = DateTime.Today.AddDays(-5);
        var input = "seen on " + recent.ToString("M/d/yyyy") + " today";
        var json = PolicyJson("SHIFT", "\"shiftDays\": 100, \"futureDates\": true");

        var filtered = new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, input).FilteredText;

        var shifted = DateTime.Parse(filtered.Replace("seen on ", "").Replace(" today", ""));
        Assert.Equal(recent.AddDays(100).Date, shifted.Date);
    }

    [Fact]
    public void FutureDates_False_LeavesADateThatWasAlreadyAheadAlone()
    {
        // The guard only keeps a past date in the past; it does not drag a future date backwards.
        var ahead = DateTime.Today.AddDays(30);
        var input = "seen on " + ahead.ToString("M/d/yyyy") + " today";
        var json = PolicyJson("SHIFT", "\"shiftDays\": 5");

        var filtered = new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, input).FilteredText;

        var shifted = DateTime.Parse(filtered.Replace("seen on ", "").Replace(" today", ""));
        Assert.Equal(ahead.AddDays(5).Date, shifted.Date);
    }

    // ---------------- TRUNCATE_TO_YEAR and RELATIVE (#109) ----------------

    [Theory]
    [InlineData("01/15/1990", "1990")]
    [InlineData("January 15, 1990", "1990")]
    public void TruncateToYear_ReplacesTheDateWithItsYear(string date, string year)
    {
        var json = PolicyJson("TRUNCATE_TO_YEAR", "\"shiftDays\": 0");

        var filtered = new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, "seen on " + date + " today")
            .FilteredText;

        Assert.Equal("seen on " + year + " today", filtered);
    }

    [Fact]
    public void Relative_PhrasesAPastDateAsAnElapsedInterval()
    {
        var threeMonthsAgo = DateTime.Today.AddMonths(-3);

        Assert.Equal("seen on 3 months ago today", RelativeOf(threeMonthsAgo, futureDates: false));
    }

    [Fact]
    public void Relative_RoundsUpFromTheFifteenthDay()
    {
        // Java adds a month once fifteen days have passed, and the phrasing follows it exactly.
        var older = DateTime.Today.AddMonths(-3).AddDays(-20);

        Assert.Equal("seen on 4 months ago today", RelativeOf(older, futureDates: false));
    }

    [Fact]
    public void Relative_IncludesYearsOnceAYearHasPassed()
    {
        var older = DateTime.Today.AddYears(-2).AddMonths(-1);

        // "1 months" rather than "1 month" is the Java filter's wording, kept for parity.
        Assert.Equal("seen on 2 years 1 months ago today", RelativeOf(older, futureDates: false));
    }

    [Fact]
    public void Relative_RedactsAFutureDateWhenFutureDatesIsOff()
    {
        var ahead = DateTime.Today.AddMonths(4);

        Assert.Equal("seen on {{{REDACTED-date}}} today", RelativeOf(ahead, futureDates: false));
    }

    [Fact]
    public void Relative_PhrasesAFutureDateWhenFutureDatesIsOn()
    {
        Assert.Equal("seen on in 4 months today", RelativeOf(DateTime.Today.AddMonths(4), futureDates: true));
        Assert.Equal("seen on in 2 years 3 months today",
            RelativeOf(DateTime.Today.AddYears(2).AddMonths(3), futureDates: true));
    }

    private static string RelativeOf(DateTime date, bool futureDates)
    {
        var json = PolicyJson("RELATIVE", "\"futureDates\": " + (futureDates ? "true" : "false"));
        return new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0,
                "seen on " + date.ToString("M/d/yyyy") + " today")
            .FilteredText;
    }

    [Fact]
    public void AnUnimplementedStrategyName_RaisesRatherThanRedacting()
    {
        // It used to fall through to redaction, so a policy asking for a year or an interval had the
        // date destroyed instead, with nothing reported. See #109.
        var json = PolicyJson("NOT_A_STRATEGY", "\"shiftDays\": 0");

        var ex = Assert.Throws<ArgumentException>(() => new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, Text));

        Assert.Contains("NOT_A_STRATEGY", ex.Message);
        Assert.Contains("TRUNCATE_TO_YEAR", ex.Message);
        Assert.Contains("RELATIVE", ex.Message);
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("Relative")]
    [InlineData("rElAtIvE")]
    public void ALowercaseStrategyName_IsAccepted(string strategy)
    {
        // Strategy names are matched without regard to case, as the Java filters do, so a policy is
        // not silently broken by its casing. The schema's enum is uppercase; this is leniency toward
        // a hand-written policy, not a second spelling to document.
        var threeMonthsAgo = DateTime.Today.AddMonths(-3).ToString("M/d/yyyy");
        var json = PolicyJson(strategy, "\"futureDates\": false");

        var filtered = new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, "seen on " + threeMonthsAgo + " today")
            .FilteredText;

        Assert.Equal("seen on 3 months ago today", filtered);
    }

    [Theory]
    [InlineData("shift")]
    [InlineData("shift_date")]
    [InlineData("truncate_to_year")]
    public void TheOtherDateStrategyNames_AreAlsoCaseInsensitive(string strategy)
    {
        Assert.DoesNotContain("REDACTED", Filter(PolicyJson(strategy, "\"shiftDays\": 30")));
    }

    [Fact]
    public void ACaseInsensitiveNameReachesTheStandardStrategiesToo()
    {
        // The date filter shares the standard switch with every other filter, so the leniency has to
        // hold on both sides of the dispatch or "relative" would work while "same" still redacted.
        Assert.Equal(Text, Filter(PolicyJson("same", "\"shiftDays\": 0")));
        Assert.Equal("seen on 0 today", Filter(PolicyJson("truncate", "\"shiftDays\": 0")));
    }

    [Fact]
    public void AStrategyEntryWithNoNameStillRedacts()
    {
        // An omitted strategy is not an unknown one; the schema defaults it to REDACT.
        var json = "{\"identifiers\":{\"date\":{\"dateFilterStrategies\":[{}]}}}";

        Assert.Equal("seen on {{{REDACTED-date}}} today", new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, Text).FilteredText);
    }

    [Theory]
    [InlineData("1/31/2026")] // a month-end date, where the day rounding matters
    [InlineData("3/1/2026")]
    public void Relative_HandlesMonthLengthBoundaries(string date)
    {
        // The period decomposition is a hand-written port of Java's Period.between, so the
        // month-end cases are worth holding: the result must parse as an interval, never a redaction.
        var json = PolicyJson("RELATIVE", "\"futureDates\": false");

        var filtered = new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, "seen on " + date + " today")
            .FilteredText;

        Assert.Matches(@"^seen on \d+ (years \d+ )?months ago today$", filtered);
    }

    [Theory]
    [InlineData("MASK", "seen on ********** today")]
    [InlineData("REDACT", "seen on {{{REDACTED-date}}} today")]
    public void TheOrdinaryStrategies_StillApplyToDates(string strategy, string expected)
    {
        var json = PolicyJson(strategy, "\"shiftDays\": 0");

        Assert.Equal(expected, new FilterService()
            .Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, Text).FilteredText);
    }

    [Theory]
    [InlineData("12-date-shift.json")]
    [InlineData("27-strategy-params.json")]
    public void SpecExample_RoundTripsWithNoFieldLost(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "SpecExamples", file);
        Assert.True(File.Exists(path), $"missing test data: {path}");

        var original = File.ReadAllText(path);
        var round = PolicySerializer.SerializeToJson(PolicySerializer.DeserializeFromJson(original));

        var before = new List<string>();
        var after = new List<string>();
        Leaves(JsonDocument.Parse(original).RootElement, string.Empty, before);
        Leaves(JsonDocument.Parse(round).RootElement, string.Empty, after);

        var lost = before.Where(leaf => !after.Contains(leaf)).ToList();
        Assert.True(lost.Count == 0, "lost on round-trip: " + string.Join(", ", lost));
    }

    private static void Leaves(JsonElement element, string path, List<string> into)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    Leaves(property.Value, path + "/" + property.Name, into);
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                    Leaves(item, path + "/" + index++, into);
                break;
            default:
                into.Add(path + " = " + element);
                break;
        }
    }
}
