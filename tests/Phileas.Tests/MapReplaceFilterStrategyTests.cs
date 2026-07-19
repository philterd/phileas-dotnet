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
using Phileas.Filters.Strategies.Rules;
using Phileas.Services;
using Phileas.Services.Generators;
using Xunit;

namespace Phileas.Tests;

public class MapReplaceFilterStrategyTests
{
    private static readonly string[] NoWindow = [];

    private static SsnFilterStrategy NewStrategy()
    {
        return new SsnFilterStrategy { Strategy = AbstractFilterStrategy.MapReplace };
    }

    private static string Replace(SsnFilterStrategy strategy, string token, string context = "ctx")
    {
        return strategy.GetReplacement(context, token, NoWindow, 0.9, null, null, null, null).Value;
    }

    [Fact]
    public void MapHit_ReturnsMappedValue_CaseInsensitiveByDefault()
    {
        var strategy = NewStrategy();
        strategy.Mappings = new Dictionary<string, string> { ["John Smith"] = "Bob Jones" };
        strategy.InitializeMappings(null);

        Assert.Equal("Bob Jones", Replace(strategy, "JOHN SMITH"));
    }

    [Fact]
    public void MapHit_CaseSensitive_DistinguishesKeys()
    {
        var strategy = NewStrategy();
        strategy.CaseSensitive = true;
        strategy.Mappings = new Dictionary<string, string> { ["John Smith"] = "Bob Jones" };
        strategy.InitializeMappings(null);

        Assert.Equal("Bob Jones", Replace(strategy, "John Smith"));
        // A differently-cased token misses the table and, with no generator, falls back to redaction.
        Assert.Contains("REDACTED", Replace(strategy, "john smith"));
    }

    [Fact]
    public void InlineMappings_OverrideFileMappings()
    {
        var strategy = NewStrategy();
        strategy.Mappings = new Dictionary<string, string> { ["key"] = "inline" };
        strategy.InitializeMappings(new Dictionary<string, string> { ["key"] = "fromfile" });

        Assert.Equal("inline", Replace(strategy, "key"));
    }

    [Fact]
    public void Miss_InvokesGenerator()
    {
        var strategy = NewStrategy();
        strategy.InitializeMappings(null);
        strategy.ReplacementGenerator = new StubGenerator((_, _) => "GENERATED");

        Assert.Equal("GENERATED", Replace(strategy, "unmapped"));
    }

    [Fact]
    public void GeneratorThrows_FallsBackToDefaultRedact()
    {
        var strategy = NewStrategy();
        strategy.InitializeMappings(null);
        strategy.ReplacementGenerator = new StubGenerator((_, _) => throw new InvalidOperationException("boom"));

        Assert.Contains("REDACTED", Replace(strategy, "unmapped"));
    }

    [Fact]
    public void GeneratorBlank_FallsBackToDefaultRedact()
    {
        var strategy = NewStrategy();
        strategy.InitializeMappings(null);
        strategy.ReplacementGenerator = new StubGenerator((_, _) => "   ");

        Assert.Contains("REDACTED", Replace(strategy, "unmapped"));
    }

    [Fact]
    public void Miss_UsesConfiguredFallbackStrategy()
    {
        var strategy = NewStrategy();
        strategy.FallbackStrategy = AbstractFilterStrategy.Mask;
        strategy.MaskCharacter = "*";
        strategy.InitializeMappings(null);

        Assert.Equal("****", Replace(strategy, "abcd"));
    }

    [Fact]
    public void GeneratorReturnsOriginalToken_IsRejected_AndFallsBack()
    {
        var strategy = NewStrategy();
        strategy.InitializeMappings(null);
        // A generator that echoes the token back (differing only in case) must not leave it effectively unredacted.
        strategy.ReplacementGenerator = new StubGenerator((token, _) => token.ToUpperInvariant());

        Assert.Contains("REDACTED", Replace(strategy, "unmapped"));
    }

    [Fact]
    public void GeneratedValueWithPii_IsRejected_AndFallsBack()
    {
        var strategy = NewStrategy();
        strategy.InitializeMappings(null);
        strategy.ReplacementGenerator = new StubGenerator((_, _) => "123-45-6789");
        strategy.ReplacementValidator = new StubValidator(containsPii: true);

        Assert.Contains("REDACTED", Replace(strategy, "unmapped"));
    }

    [Fact]
    public void GeneratedValueWithoutPii_IsAccepted()
    {
        var strategy = NewStrategy();
        strategy.InitializeMappings(null);
        strategy.ReplacementGenerator = new StubGenerator((_, _) => "Clean Value");
        strategy.ReplacementValidator = new StubValidator(containsPii: false);

        Assert.Equal("Clean Value", Replace(strategy, "unmapped"));
    }

    [Fact]
    public void GeneratedValue_IsCachedPerContext_AndNotReinvoked()
    {
        var generator = new StubGenerator((_, _) => null!, useCounter: true);
        var strategy = NewStrategy();
        strategy.ReplacementScope = AbstractFilterStrategy.ReplacementScopeContext;
        strategy.ContextService = new InMemoryContextService();
        strategy.InitializeMappings(null);
        strategy.ReplacementGenerator = generator;

        var first = Replace(strategy, "Acme Inc");
        var second = Replace(strategy, "Acme Inc");

        Assert.Equal("GENERATED-1", first);
        Assert.Equal(first, second);
        Assert.Equal(1, generator.Calls);
    }

    [Fact]
    public void GeneratedValue_DocumentScope_ReinvokesEachTime()
    {
        var generator = new StubGenerator((_, _) => null!, useCounter: true);
        var strategy = NewStrategy(); // default ReplacementScope is DOCUMENT
        strategy.ContextService = new InMemoryContextService();
        strategy.InitializeMappings(null);
        strategy.ReplacementGenerator = generator;

        Assert.Equal("GENERATED-1", Replace(strategy, "Acme Inc"));
        Assert.Equal("GENERATED-2", Replace(strategy, "Acme Inc"));
        Assert.Equal(2, generator.Calls);
    }

    private sealed class StubGenerator : IReplacementGenerator
    {
        private readonly Func<string, string?, string> _fn;
        private readonly bool _useCounter;

        public int Calls { get; private set; }

        public StubGenerator(Func<string, string?, string> fn, bool useCounter = false)
        {
            _fn = fn;
            _useCounter = useCounter;
        }

        public string Generate(string token, string? label)
        {
            Calls++;
            return _useCounter ? $"GENERATED-{Calls}" : _fn(token, label);
        }
    }

    private sealed class StubValidator : IReplacementValidator
    {
        private readonly bool _containsPii;

        public StubValidator(bool containsPii)
        {
            _containsPii = containsPii;
        }

        public bool ContainsPii(string candidate) => _containsPii;
    }
}
