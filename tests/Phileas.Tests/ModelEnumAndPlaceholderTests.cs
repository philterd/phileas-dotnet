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
using Phileas.Policy;
using Xunit;

namespace Phileas.Tests;

public class FilterTypeTests
{
    [Fact]
    public void CreditCardTypeName()
    {
        Assert.Equal("credit-card", FilterType.CreditCard.GetFilterTypeName());
    }
}

public class SensitivityLevelTests
{
    [Theory]
    [InlineData("low", SensitivityLevel.Low)]
    [InlineData("medium", SensitivityLevel.Medium)]
    [InlineData("high", SensitivityLevel.High)]
    [InlineData("off", SensitivityLevel.Off)]
    [InlineData("auto", SensitivityLevel.Auto)]
    [InlineData("bogus", SensitivityLevel.High)] // unknown defaults to high
    public void FromName(string name, SensitivityLevel expected)
    {
        Assert.Equal(expected, SensitivityLevels.FromName(name));
    }
}

public class PlaceholderDeserializerTests
{
    [Fact]
    public void PlaceholderResolvesFromEnvironment()
    {
        const string name = "PHILEAS_PLACEHOLDER_TEST";
        Environment.SetEnvironmentVariable(name, "resolved-value");
        try
        {
            const string json = """
                                {
                                  "ignored": [ { "terms": [ "${PHILEAS_PLACEHOLDER_TEST}" ] } ]
                                }
                                """;

            var policy = PolicySerializer.DeserializeFromJson(json);

            Assert.Single(policy.Ignored);
            Assert.Equal("resolved-value", policy.Ignored[0].Terms[0]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void UnknownPlaceholderIsLeftAsIs()
    {
        const string json = """
                            {
                              "ignored": [ { "terms": [ "${PHILEAS_NO_SUCH_VARIABLE}" ] } ]
                            }
                            """;

        var policy = PolicySerializer.DeserializeFromJson(json);

        Assert.Equal("${PHILEAS_NO_SUCH_VARIABLE}", policy.Ignored[0].Terms[0]);
    }

    [Fact]
    public void NonPlaceholderStringsAreUntouched()
    {
        const string json = """
                            {
                              "ignored": [ { "terms": [ "just-a-term" ] } ]
                            }
                            """;

        var policy = PolicySerializer.DeserializeFromJson(json);

        Assert.Equal("just-a-term", policy.Ignored[0].Terms[0]);
    }
}
