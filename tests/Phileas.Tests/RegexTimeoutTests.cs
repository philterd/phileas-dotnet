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

using System.Diagnostics;
using Phileas.Filters;
using Phileas.Filters.PostFilters;
using Phileas.Filters.Rules.Regex.RegexFilters;
using Phileas.Filters.Strategies.Rules;
using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
///     Regex match budgets. See philterd/phileas-dotnet#98.
/// </summary>
public class RegexTimeoutTests
{
    // A pattern whose nested quantifier backtracks exponentially when the subject cannot match,
    // paired with a subject that forces it. Without a budget this runs effectively forever.
    private const string CatastrophicPattern = @"^(a+)+$";
    private static readonly string CatastrophicInput = new string('a', 60) + "!";

    private static FilterConfiguration Config(long timeoutMs, IList<IgnoredPattern>? ignoredPatterns = null)
    {
        return new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new IdentifierFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(ignoredPatterns ?? new List<IgnoredPattern>())
            .WithRegexTimeoutMs(timeoutMs)
            .Build();
    }

    [Fact]
    public void DefaultBudget_IsUnchangedAtOneSecond()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(1000), RegexDefaults.MatchTimeout);
        Assert.Equal(1000, new FilterConfiguration.Builder().Build().RegexTimeoutMs);
        Assert.Equal(RegexDefaults.MatchTimeout, FilterPattern.DefaultMatchTimeout);
    }

    [Theory]
    [InlineData(0)] // .NET rejects a zero budget outright
    [InlineData(-1)] // .NET's "no timeout" sentinel, which would remove the bound entirely
    public void NonPositiveBudget_IsRejectedWhereItIsConfigured(long ms)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new FilterConfiguration.Builder().WithRegexTimeoutMs(ms));
        Assert.Equal("regexTimeoutMs", ex.ParamName);
    }

    [Fact]
    public void BuiltInFilter_HonoursTheConfiguredBudget()
    {
        // The budget has to reach the built-in patterns, not just the user-supplied ones.
        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { new SsnFilterStrategy() })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .WithRegexTimeoutMs(1)
            .Build();
        var filter = new SsnFilter(config);
        var policy = new PhileasPolicy { Name = "t", Identifiers = new Identifiers { Ssn = new Ssn() } };
        var large = string.Join(" ", Enumerable.Repeat("078-05-1120 filler text here", 20000));

        var filtered = filter.Filter(policy, "ctx", 0, large);

        Assert.Empty(filtered.Spans);
        var timeouts = filter.DrainRegexTimeouts();
        Assert.Single(timeouts);
        Assert.Contains("Ssn", timeouts[0]);
    }

    [Fact]
    public void BuiltInPattern_CarriesTheDefaultBudget()
    {
        var pattern = new FilterPattern.Builder().WithPattern(@"\d{3}").Build();

        Assert.Equal(RegexDefaults.MatchTimeout, pattern.Pattern.MatchTimeout);
        Assert.NotEqual(System.Text.RegularExpressions.Regex.InfiniteMatchTimeout,
            pattern.Pattern.MatchTimeout);
    }

    [Fact]
    public void GetPattern_ReusesTheCompiledInstanceForTheSameBudget()
    {
        var pattern = new FilterPattern.Builder().WithPattern(@"\d{3}").Build();

        // The default path must not recompile: patterns live in static fields and are reused across
        // every request, so a per-call rebuild would be a throughput regression.
        Assert.Same(pattern.Pattern, pattern.GetPattern(RegexDefaults.MatchTimeout));

        var shorter = pattern.GetPattern(TimeSpan.FromMilliseconds(5));
        Assert.NotSame(pattern.Pattern, shorter);
        Assert.Equal(TimeSpan.FromMilliseconds(5), shorter.MatchTimeout);
        Assert.Same(shorter, pattern.GetPattern(TimeSpan.FromMilliseconds(5)));
    }

    [Fact]
    public void CatastrophicIdentifierPattern_TimesOutAndIsReported()
    {
        var filter = new IdentifierFilter(Config(20), "CUSTOM", CatastrophicPattern, false, 0);
        var policy = new PhileasPolicy
        {
            Name = "t",
            Identifiers = new Identifiers
            {
                CustomIdentifiers = new List<Identifier>
                {
                    new() { Classification = "CUSTOM", Pattern = CatastrophicPattern }
                }
            }
        };

        var stopwatch = Stopwatch.StartNew();
        var filtered = filter.Filter(policy, "ctx", 0, CatastrophicInput);
        stopwatch.Stop();

        Assert.Empty(filtered.Spans);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"the match was not abandoned; it took {stopwatch.Elapsed}");

        // The point of the issue: no spans plus no signal is indistinguishable from a clean document.
        var timeouts = filter.DrainRegexTimeouts();
        Assert.Single(timeouts);
        Assert.Contains(CatastrophicPattern, timeouts[0]);
    }

    [Fact]
    public void DrainRegexTimeouts_ClearsSoEachPieceReportsSeparately()
    {
        var filter = new IdentifierFilter(Config(20), "CUSTOM", CatastrophicPattern, false, 0);
        var policy = new PhileasPolicy
        {
            Name = "t",
            Identifiers = new Identifiers
            {
                CustomIdentifiers = new List<Identifier>
                {
                    new() { Classification = "CUSTOM", Pattern = CatastrophicPattern }
                }
            }
        };

        filter.Filter(policy, "ctx", 0, CatastrophicInput);
        Assert.Single(filter.DrainRegexTimeouts());
        Assert.Empty(filter.DrainRegexTimeouts());
    }

    [Fact]
    public void FilterService_SurfacesTheTimeoutOnTheResult()
    {
        var policy = new PhileasPolicy
        {
            Name = "t",
            Identifiers = new Identifiers
            {
                Ssn = new Ssn(),
                CustomIdentifiers = new List<Identifier>
                {
                    new() { Classification = "CUSTOM", Pattern = CatastrophicPattern }
                }
            }
        };

        var result = new FilterService().Filter(policy, "ctx", 0, CatastrophicInput);

        // At the default budget the catastrophic pattern still gives up, and says so.
        Assert.NotEmpty(result.RegexTimeouts);
        Assert.Contains(result.RegexTimeouts, t => t.Contains(CatastrophicPattern));
    }

    [Fact]
    public void FilterService_ReportsNoTimeoutsForAnOrdinaryDocument()
    {
        var policy = new PhileasPolicy { Name = "t", Identifiers = new Identifiers { Ssn = new Ssn() } };

        var result = new FilterService().Filter(policy, "ctx", 0, "The SSN is 078-05-1120.");

        Assert.Empty(result.RegexTimeouts);
        Assert.Equal("The SSN is {{{REDACTED-ssn}}}.", result.FilteredText);
    }

    [Fact]
    public void IgnoredPattern_CannotHangFiltering_AndKeepsTheSpan()
    {
        var span = Span.Make(0, CatastrophicInput.Length, FilterType.Ssn, "ctx", 0.9,
            CatastrophicInput, "{{{REDACTED-ssn}}}", string.Empty, false, true,
            Array.Empty<string>(), 0, null);
        var ignored = new List<IgnoredPattern>
        {
            new() { Pattern = CatastrophicPattern }
        };
        var reported = new List<string>();

        var stopwatch = Stopwatch.StartNew();
        var kept = IgnoredPatternsPostFilter.Apply(new List<Span> { span }, ignored,
            TimeSpan.FromMilliseconds(20), reported.Add);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"an ignoredPattern hung the post-filter for {stopwatch.Elapsed}");

        // Fail closed: an ignored pattern that could not be evaluated must not drop a detection,
        // which would leave the value in the clear.
        Assert.Single(kept);
        Assert.Single(reported);
        Assert.Contains(CatastrophicPattern, reported[0]);
    }

    [Fact]
    public void IgnoredPattern_ThatEvaluatesInTime_StillDropsTheSpan()
    {
        var span = Span.Make(0, 11, FilterType.Ssn, "ctx", 0.9, "078-05-1120",
            "{{{REDACTED-ssn}}}", string.Empty, false, true, Array.Empty<string>(), 0, null);
        var ignored = new List<IgnoredPattern> { new() { Pattern = @"^078" } };
        var reported = new List<string>();

        var kept = IgnoredPatternsPostFilter.Apply(new List<Span> { span }, ignored, null, reported.Add);

        Assert.Empty(kept);
        Assert.Empty(reported);
    }
}
