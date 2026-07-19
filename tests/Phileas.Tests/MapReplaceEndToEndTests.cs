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
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

public class MapReplaceEndToEndTests
{
    private static PhileasPolicy SsnMapReplacePolicy(SsnFilterStrategy strategy, Dictionary<string, Generator>? generators = null)
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers { Ssn = new Ssn { Strategies = [strategy] } },
            Generators = generators
        };
    }

    [Fact]
    public void MapReplace_ReplacesFromMappingFile()
    {
        var mappingFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(mappingFile, "123-45-6789\tMAPPED-VALUE\n");

            var policy = SsnMapReplacePolicy(new SsnFilterStrategy
            {
                Strategy = AbstractFilterStrategy.MapReplace,
                MappingFiles = [mappingFile]
            });

            var result = new FilterService().Filter(policy, "test", 0, "SSN: 123-45-6789");

            Assert.Contains("MAPPED-VALUE", result.FilteredText);
            Assert.DoesNotContain("123-45-6789", result.FilteredText);
        }
        finally
        {
            File.Delete(mappingFile);
        }
    }

    [Fact]
    public void MapReplace_InlineMappingOverridesFile()
    {
        var mappingFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(mappingFile, "123-45-6789\tFROM-FILE\n");

            var policy = SsnMapReplacePolicy(new SsnFilterStrategy
            {
                Strategy = AbstractFilterStrategy.MapReplace,
                MappingFiles = [mappingFile],
                Mappings = new Dictionary<string, string> { ["123-45-6789"] = "FROM-INLINE" }
            });

            var result = new FilterService().Filter(policy, "test", 0, "SSN: 123-45-6789");

            Assert.Contains("FROM-INLINE", result.FilteredText);
            Assert.DoesNotContain("FROM-FILE", result.FilteredText);
        }
        finally
        {
            File.Delete(mappingFile);
        }
    }

    [Fact]
    public void MapReplace_MissWithNoGenerator_FallsBackToRedact()
    {
        var policy = SsnMapReplacePolicy(new SsnFilterStrategy { Strategy = AbstractFilterStrategy.MapReplace });

        var result = new FilterService().Filter(policy, "test", 0, "SSN: 123-45-6789");

        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("123-45-6789", result.FilteredText);
    }

    [Fact]
    public void MapReplace_UnresolvedGeneratorName_FallsBackToConfiguredStrategy()
    {
        var policy = SsnMapReplacePolicy(new SsnFilterStrategy
        {
            Strategy = AbstractFilterStrategy.MapReplace,
            Generator = "does-not-exist",
            FallbackStrategy = AbstractFilterStrategy.Mask,
            MaskCharacter = "#"
        });

        var result = new FilterService().Filter(policy, "test", 0, "SSN: 123-45-6789");

        Assert.Contains("###########", result.FilteredText); // 123-45-6789 is 11 characters
        Assert.DoesNotContain("123-45-6789", result.FilteredText);
    }
}
