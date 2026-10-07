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

using System.Reflection;
using Phileas.Filters.PhEye;
using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;
using RuntimeStrategy = Phileas.Filters.AbstractFilterStrategy;

namespace Phileas.Tests;

/// <summary>
///     PhEye strategies and configuration are built the same way as every other filter's, so every strategy
///     setting, including MAP_REPLACE's, and the policy's crypto and fpe settings reach the PhEye filter.
///     See philterd/phileas-dotnet#161.
/// </summary>
public class PhEyeStrategyBuildTests
{
    private static PhEyeFilter BuildPhEyeFilter(PhileasPolicy policy)
    {
        var build = typeof(FilterService).GetMethod("BuildFilters", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var filters = (IEnumerable<Phileas.Filters.AbstractFilter>)build.Invoke(new FilterService(),
            new object[] { policy, new InMemoryContextService() })!;
        return Assert.Single(filters.OfType<PhEyeFilter>());
    }

    private static PhileasPolicy PolicyWith(PhEyeFilterStrategy strategy) => new()
    {
        Name = "p",
        Identifiers = new Identifiers
        {
            PhEyes = new List<PhEye>
            {
                new()
                {
                    PhEyeConfiguration = new PhEyeConfiguration { Endpoint = "http://localhost:1" },
                    Strategies = new List<PhEyeFilterStrategy> { strategy }
                }
            }
        }
    };

    // A value for a property of the given type that differs from its default, so a property left uncopied
    // shows up as a mismatch.
    private static object? ValueFor(Type type, string name) => type switch
    {
        _ when type == typeof(string) => "value-" + name,
        _ when type == typeof(bool) || type == typeof(bool?) => true,
        _ when type == typeof(int) || type == typeof(int?) => 7,
        _ when type == typeof(List<string>) => new List<string> { "value-" + name },
        _ when type == typeof(Dictionary<string, string>) => new Dictionary<string, string> { ["a"] = "b" },
        _ => throw new InvalidOperationException(
            $"Add a value for {name} ({type}) so this test covers it.")
    };

    [Fact]
    public void EveryStrategySettingReachesThePhEyeRuntimeStrategy()
    {
        // Drift-proof: a setting added to the policy strategy model is covered without changing this test.
        var policyStrategy = new PhEyeFilterStrategy();
        var shared = typeof(RuntimeStrategy).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && typeof(PhEyeFilterStrategy).GetProperty(p.Name) is { CanWrite: true })
            .Select(p => typeof(PhEyeFilterStrategy).GetProperty(p.Name)!)
            .ToList();
        foreach (var property in shared)
            property.SetValue(policyStrategy, ValueFor(property.PropertyType, property.Name));

        var runtime = Assert.Single(BuildPhEyeFilter(PolicyWith(policyStrategy)).GetStrategies());

        Assert.NotEmpty(shared);
        foreach (var property in shared)
        {
            var expected = property.GetValue(policyStrategy);
            var actual = typeof(RuntimeStrategy).GetProperty(property.Name)!.GetValue(runtime);
            Assert.True(Equals(expected, actual), $"{property.Name} was not carried to the PhEye strategy");
        }
    }

    [Fact]
    public void ThePolicysCryptoAndFpeSettingsReachThePhEyeFilter()
    {
        // Without them CRYPTO_REPLACE on a PhEye filter threw "Missing crypto encryption property" and
        // failed the whole filter call.
        var policy = PolicyWith(new PhEyeFilterStrategy { Strategy = "CRYPTO_REPLACE" });
        policy.Crypto = new Crypto { Key = "9EE7A356FDFE43F069500B0086758346E66D8583E0CE1CFCA04E50F67ECCE5D1" };
        policy.Fpe = new Fpe { Key = "2DE79D232DF5585D68CE47882AE256D6", Tweak = "CBD09280979564" };

        var filter = BuildPhEyeFilter(policy);

        object? Field(string name) => typeof(Phileas.Filters.AbstractFilter)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(filter);
        Assert.Same(policy.Crypto, Field("Crypto"));
        Assert.Same(policy.Fpe, Field("Fpe"));
    }

    [Fact]
    public void AMapReplaceStrategysMappingTableIsResolved()
    {
        // The table is built from the inline mappings (and any mapping files) when the strategy is built, so a
        // strategy that skipped that step had nothing to look a name up in. Checked without a model by asking
        // the built strategy directly.
        var runtime = Assert.Single(BuildPhEyeFilter(PolicyWith(new PhEyeFilterStrategy
        {
            Strategy = "MAP_REPLACE",
            Mappings = new Dictionary<string, string> { ["George Washington"] = "Alex Smith" }
        })).GetStrategies());

        var replacement = runtime.GetReplacement("ctx", "George Washington", Array.Empty<string>(), 0.9, "name",
            null, null, null);

        Assert.Equal("Alex Smith", replacement.Value);
    }

    // --- End to end with a local model ------------------------------------------------------------------

    private static PhileasPolicy ModelPolicy(PhEyeFilterStrategy strategy, string dir)
    {
        var policy = PolicyWith(strategy);
        policy.Identifiers.PhEyes![0].PhEyeConfiguration =
            new PhEyeConfiguration { ModelPath = dir, Labels = new List<string> { "name" }, Threshold = 0.5 };
        return policy;
    }

    private const string Text = "Please contact George Washington today.";

    [DownloadModelFact]
    public void MapReplace_UsesTheMapping()
    {
        var policy = ModelPolicy(new PhEyeFilterStrategy
        {
            Strategy = "MAP_REPLACE",
            Mappings = new Dictionary<string, string> { ["George Washington"] = "Alex Smith" }
        }, XsmallModel.EnsureDownloaded());

        Assert.Equal("Please contact Alex Smith today.", new FilterService().Filter(policy, "ctx", 0, Text).FilteredText);
    }

    [DownloadModelFact]
    public void MapReplace_UsesItsFallbackForAnUnmappedName()
    {
        var policy = ModelPolicy(new PhEyeFilterStrategy { Strategy = "MAP_REPLACE", FallbackStrategy = "MASK" },
            XsmallModel.EnsureDownloaded());

        Assert.Equal("Please contact ***************** today.",
            new FilterService().Filter(policy, "ctx", 0, Text).FilteredText);
    }

    [DownloadModelFact]
    public void CryptoReplace_Encrypts()
    {
        var policy = ModelPolicy(new PhEyeFilterStrategy { Strategy = "CRYPTO_REPLACE" }, XsmallModel.EnsureDownloaded());
        policy.Crypto = new Crypto { Key = "9EE7A356FDFE43F069500B0086758346E66D8583E0CE1CFCA04E50F67ECCE5D1" };

        var filtered = new FilterService().Filter(policy, "ctx", 0, Text).FilteredText;

        Assert.StartsWith("Please contact {{", filtered);
        Assert.DoesNotContain("George", filtered);
    }
}
