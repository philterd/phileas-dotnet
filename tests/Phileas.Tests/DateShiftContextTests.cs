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

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Phileas.Filters;
using Phileas.Filters.Strategies.Rules;
using Phileas.Model;
using Phileas.Policy;
using Phileas.Services;
using Xunit;

namespace Phileas.Tests;

/// <summary>
///     A random date shift with the CONTEXT replacement scope, and the 30-day minimum on a random shift.
///     See philterd/phileas-dotnet#202 and, for the Java port, philterd/phileas#514 and philterd/phileas#518.
/// </summary>
public class DateShiftContextTests
{
    private static DateFilterStrategy RandomShift(string scope, IContextService? contextService)
    {
        return new DateFilterStrategy
        {
            Strategy = AbstractFilterStrategy.Shift,
            ShiftRandom = true,
            ReplacementScope = scope,
            ContextService = contextService
        };
    }

    private static DateTime Shift(DateFilterStrategy strategy, string context, string date)
    {
        var replacement = strategy.GetReplacement(context, date, [], 0.9, null, null, null, null);
        return DateTime.Parse(replacement.Value, CultureInfo.InvariantCulture);
    }

    private static DateTime Date(string date)
    {
        return DateTime.Parse(date, CultureInfo.InvariantCulture);
    }

    [Fact]
    public void ContextScope_MovesEveryDateInAContextByTheSameNumberOfDays()
    {
        var strategy = RandomShift(AbstractFilterStrategy.ReplacementScopeContext, new InMemoryContextService());

        for (var i = 0; i < 20; i++)
        {
            var context = "patient-" + i;
            var admitted = Shift(strategy, context, "01/05/2020");
            var surgery = Shift(strategy, context, "01/07/2020");
            var followUp = Shift(strategy, context, "03/15/2021");

            Assert.Equal(2, (surgery - admitted).Days);
            Assert.Equal((Date("03/15/2021") - Date("01/05/2020")).Days, (followUp - admitted).Days);
        }
    }

    [Fact]
    public void ContextScope_KeepsJanuary30And31ADayApart()
    {
        // Adding a month moves both to the end of February, so the shift is kept in days.
        var strategy = RandomShift(AbstractFilterStrategy.ReplacementScopeContext, new InMemoryContextService());

        for (var i = 0; i < 20; i++)
        {
            var context = "patient-" + i;
            var first = Shift(strategy, context, "01/30/2020");
            var second = Shift(strategy, context, "01/31/2020");

            Assert.Equal(1, (second - first).Days);
        }
    }

    [Fact]
    public void ContextScope_TheSameDateGivesTheSameResultEveryTime()
    {
        var strategy = RandomShift(AbstractFilterStrategy.ReplacementScopeContext, new InMemoryContextService());

        var first = Shift(strategy, "patient-1", "01/05/2020");
        for (var i = 0; i < 10; i++)
            Assert.Equal(first, Shift(strategy, "patient-1", "01/05/2020"));
    }

    [Fact]
    public void ContextScope_DifferentContextsDrawTheirOwnShift()
    {
        var strategy = RandomShift(AbstractFilterStrategy.ReplacementScopeContext, new InMemoryContextService());

        var shifted = Enumerable.Range(0, 20)
            .Select(i => Shift(strategy, "patient-" + i, "01/05/2020"))
            .ToHashSet();

        Assert.True(shifted.Count > 1, "every context drew the same shift");
    }

    [Fact]
    public void ContextScope_KeepsTheShiftAcrossDocuments()
    {
        // The FilterService is given one context service, so the second document is filtered in the same
        // context as the first.
        const string json = "{\"identifiers\":{\"date\":{\"dateFilterStrategies\":[{\"strategy\":\"SHIFT\","
                            + "\"shiftRandom\":true,\"replacementScope\":\"CONTEXT\"}]}}}";
        var policy = PolicySerializer.DeserializeFromJson(json);
        var service = new FilterService(new InMemoryContextService());

        var first = service.Filter(policy, "patient-1", 0, "Admitted 01/05/2020. Surgery 01/07/2020.").FilteredText;
        var second = service.Filter(policy, "patient-1", 0, "Discharged 01/20/2020.").FilteredText;

        var dates = Regex.Matches(first + " " + second, @"\d{1,2}/\d{1,2}/\d{4}")
            .Select(m => Date(m.Value))
            .ToList();

        Assert.Equal(3, dates.Count);
        Assert.Equal(2, (dates[1] - dates[0]).Days);
        Assert.Equal(15, (dates[2] - dates[0]).Days);
        Assert.True(dates[0] <= Date("01/05/2020").AddDays(-DateFilterStrategy.MinRandomShiftDays));
    }

    [Fact]
    public void ContextScope_StoresTheShiftOnceAsDaysUnderAKeyNoTokenCanBe()
    {
        // A context service that only implements Get and Put gets the default ComputeValueIfAbsent.
        var contextService = new RecordingContextService();
        var strategy = RandomShift(AbstractFilterStrategy.ReplacementScopeContext, contextService);

        var shifted = Shift(strategy, "patient-1", "01/05/2020");
        Shift(strategy, "patient-1", "01/07/2020");
        Shift(strategy, "patient-1", "01/05/2020");

        var put = Assert.Single(contextService.Puts);
        Assert.Equal("patient-1", put.Context);
        Assert.Equal(DateFilterStrategy.ContextShiftKey, put.Key);
        Assert.Contains('\0', put.Key);
        Assert.Equal((shifted - Date("01/05/2020")).Days,
            int.Parse(put.Value, NumberStyles.Integer, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ContextScope_ConcurrentFirstUseOfAContextDrawsOneShift()
    {
        var strategy = RandomShift(AbstractFilterStrategy.ReplacementScopeContext, new InMemoryContextService());

        for (var round = 0; round < 10; round++)
        {
            var context = "patient-" + round;
            var shifts = new ConcurrentBag<int>();

            Parallel.For(0, 32, new ParallelOptions { MaxDegreeOfParallelism = 32 }, i =>
            {
                var date = new DateTime(2020, 1, 1).AddDays(i);
                var token = date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
                shifts.Add((Shift(strategy, context, token) - date).Days);
            });

            Assert.Single(shifts.Distinct());
        }
    }

    [Fact]
    public void DocumentScope_ShiftsEachDateByItsOwnAmount()
    {
        var contextService = new RecordingContextService();
        var strategy = RandomShift(AbstractFilterStrategy.ReplacementScopeDocument, contextService);

        var shifted = Enumerable.Range(0, 40)
            .Select(_ => Shift(strategy, "patient-1", "01/05/2020"))
            .ToHashSet();

        Assert.True(shifted.Count > 1, "a random shift should not produce one value every time");
        Assert.Empty(contextService.Puts);
    }

    [Fact]
    public void ContextScope_WithoutAContextService_ShiftsEachDateByItsOwnAmount()
    {
        var strategy = RandomShift(AbstractFilterStrategy.ReplacementScopeContext, null);

        var shifted = Enumerable.Range(0, 40)
            .Select(_ => Shift(strategy, "patient-1", "01/05/2020"))
            .ToHashSet();

        Assert.True(shifted.Count > 1, "a random shift should not produce one value every time");
    }

    [Theory]
    [InlineData(AbstractFilterStrategy.ReplacementScopeDocument)]
    [InlineData(AbstractFilterStrategy.ReplacementScopeContext)]
    public void ARandomShift_NeverMovesADateFewerThan30Days(string scope)
    {
        // 29 days and 11 months forward and a year back from 2021-02-28 gives 2021-02-28.
        var strategy = RandomShift(scope, new InMemoryContextService());
        var date = Date("02/28/2021");

        for (var i = 0; i < 1000; i++)
        {
            var days = Math.Abs((Shift(strategy, "patient-" + i, "02/28/2021") - date).Days);
            Assert.True(days >= DateFilterStrategy.MinRandomShiftDays, $"moved by {days} days");
        }
    }

    [Fact]
    public void ContextScope_DoesNotChangeAFixedShift()
    {
        var contextService = new RecordingContextService();
        var strategy = new DateFilterStrategy
        {
            Strategy = AbstractFilterStrategy.Shift,
            ShiftDays = 10,
            ShiftMonths = 1,
            ShiftYears = -1,
            ReplacementScope = AbstractFilterStrategy.ReplacementScopeContext,
            ContextService = contextService
        };

        Assert.Equal("2/25/1989", strategy.GetReplacement("ctx", "01/15/1990", [], 0.9, null, null, null, null).Value);
        Assert.Empty(contextService.Puts);
    }

    [Fact]
    public void InMemoryContextService_ComputeValueIfAbsent_CallsTheFactoryOncePerKey()
    {
        var contextService = new InMemoryContextService();
        var calls = 0;
        var values = new ConcurrentBag<string>();

        Parallel.For(0, 32, new ParallelOptions { MaxDegreeOfParallelism = 32 }, i =>
            values.Add(contextService.ComputeValueIfAbsent("ctx", "key", FilterType.Date, () =>
            {
                Interlocked.Increment(ref calls);
                Thread.Sleep(5);
                return i.ToString(CultureInfo.InvariantCulture);
            })));

        Assert.Equal(1, calls);
        Assert.Single(values.Distinct());
        Assert.Equal(values.First(), contextService.Get("ctx", "key"));
        Assert.Equal("other", contextService.ComputeValueIfAbsent("ctx2", "key", FilterType.Date, () => "other"));
    }

    [Fact]
    public void TheDefaultComputeValueIfAbsent_StoresTheValueThroughPut()
    {
        IContextService contextService = new RecordingContextService();

        Assert.Equal("1", contextService.ComputeValueIfAbsent("ctx", "key", FilterType.Date, () => "1"));
        Assert.Equal("1", contextService.ComputeValueIfAbsent("ctx", "key", FilterType.Date, () => "2"));
        Assert.Equal("1", contextService.Get("ctx", "key"));
        Assert.Single(((RecordingContextService)contextService).Puts);
    }

    /// <summary>A context service that implements only Get and Put, and records each Put.</summary>
    private sealed class RecordingContextService : IContextService
    {
        private readonly Dictionary<(string, string), string> _values = new();

        public List<(string Context, string Key, string Value)> Puts { get; } = new();

        public string? Get(string contextName, string token)
        {
            return _values.TryGetValue((contextName, token), out var value) ? value : null;
        }

        public void Put(string contextName, string token, string replacement)
        {
            Puts.Add((contextName, token, replacement));
            _values[(contextName, token)] = replacement;
        }
    }
}
