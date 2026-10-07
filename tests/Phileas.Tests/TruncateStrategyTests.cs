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
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;
using RuntimeStrategy = Phileas.Filters.AbstractFilterStrategy;

namespace Phileas.Tests;

/// <summary>
///     The <c>TRUNCATE</c> strategy honours <c>truncateLeaveCharacters</c>, <c>truncateDirection</c> and
///     <c>truncateCharacter</c>, as the Java port does. See philterd/phileas-dotnet#160.
/// </summary>
public class TruncateStrategyTests
{
    private const string Card = "Card 4111111111111111 on file.";

    private static string CardPolicy(string settings) =>
        "{\"identifiers\":{\"creditCard\":{\"creditCardFilterStrategies\":[{\"strategy\":\"TRUNCATE\"" + settings + "}]}}}";

    private static string Filter(string policyJson, string input) =>
        new FilterService().Filter(PolicySerializer.DeserializeFromJson(policyJson), "ctx", 0, input).FilteredText;

    private static string Filter(PhileasPolicy policy, string input) =>
        new FilterService().Filter(policy, "ctx", 0, input).FilteredText;

    // Each row is the Java port's output for the same policy and input.
    [Theory]
    [InlineData("", "Card 4111************ on file.")]
    [InlineData(",\"truncateLeaveCharacters\":4", "Card 4111************ on file.")]
    [InlineData(",\"truncateLeaveCharacters\":4,\"truncateDirection\":\"TRAILING\"", "Card ************1111 on file.")]
    [InlineData(",\"truncateLeaveCharacters\":4,\"truncateDirection\":\"TRAILING\",\"truncateCharacter\":\"#\"", "Card ############1111 on file.")]
    [InlineData(",\"truncateLeaveCharacters\":2", "Card 41************** on file.")]
    [InlineData(",\"truncateDirection\":\"LEADING\"", "Card 4111************ on file.")]
    [InlineData(",\"truncateLeaveCharacters\":4,\"truncateCharacter\":\"\"", "Card 4111 on file.")]
    public void Truncate_MatchesTheJavaPort(string settings, string expected)
    {
        Assert.Equal(expected, Filter(CardPolicy(settings), Card));
    }

    [Fact]
    public void TheSettingsSurviveARoundTrip()
    {
        var json = CardPolicy(",\"truncateLeaveCharacters\":4,\"truncateDirection\":\"TRAILING\",\"truncateCharacter\":\"#\"");

        var written = PolicySerializer.SerializeToJson(PolicySerializer.DeserializeFromJson(json));

        Assert.Contains("\"truncateLeaveCharacters\":4", written);
        Assert.Contains("\"truncateDirection\":\"TRAILING\"", written);
        Assert.Contains("\"truncateCharacter\":\"#\"", written);
        Assert.Equal("Card ############1111 on file.", Filter(written, Card));
    }

    [Fact]
    public void UnsetSettingsAreNotWritten()
    {
        // The defaults are applied when truncating, so a strategy that sets nothing gains no keys.
        var written = PolicySerializer.SerializeToJson(PolicySerializer.DeserializeFromJson(CardPolicy("")));

        Assert.DoesNotContain("truncate", written);
    }

    // At most length - 1 characters are kept, so a value no longer than the characters to leave still has at least
    // one character replaced, as in the Java port. See philterd/phileas-dotnet#163.
    private static string TruncateWord(string word, string settings) =>
        Filter("{\"identifiers\":{\"identifiers\":[{\"pattern\":\"\\\\b[A-Z][A-Z0-9]*\\\\b\","
               + "\"identifierFilterStrategies\":[{\"strategy\":\"TRUNCATE\"" + settings + "}]}]}}", $"value {word} end");

    [Theory]
    [InlineData("AB", "", "A*")]
    [InlineData("ABCD", "", "ABC*")]
    [InlineData("ABCD1234", "", "ABCD****")]
    [InlineData("AB", ",\"truncateDirection\":\"TRAILING\"", "*B")]
    [InlineData("ABCD", ",\"truncateDirection\":\"TRAILING\"", "*BCD")]
    [InlineData("ABCD1234", ",\"truncateDirection\":\"TRAILING\"", "****1234")]
    [InlineData("A", ",\"truncateLeaveCharacters\":2", "*")]
    [InlineData("ABC", ",\"truncateLeaveCharacters\":2", "AB*")]
    [InlineData("ABC", ",\"truncateLeaveCharacters\":2,\"truncateDirection\":\"TRAILING\"", "*BC")]
    [InlineData("ABCDE", ",\"truncateLeaveCharacters\":4", "ABCD*")]
    [InlineData("ABCDE", ",\"truncateLeaveCharacters\":4,\"truncateDirection\":\"TRAILING\"", "*BCDE")]
    public void AtLeastOneCharacterIsAlwaysReplaced(string word, string settings, string expected)
    {
        Assert.Equal($"value {expected} end", TruncateWord(word, settings));
    }

    [Fact]
    public void FewerThanOneCharacterToLeave_IsOne()
    {
        // The schema rejects 0 in JSON, so this is set in code.
        var policy = new PhileasPolicy
        {
            Name = "p",
            Identifiers = new Identifiers
            {
                CustomIdentifiers = new List<Identifier>
                {
                    new()
                    {
                        Pattern = @"\b[A-Z][A-Z0-9]*\b",
                        Strategies = new List<IdentifierFilterStrategy>
                            { new() { Strategy = "TRUNCATE", TruncateLeaveCharacters = 0 } }
                    }
                }
            }
        };

        Assert.Equal("value A*** end", Filter(policy, "value ABCD end"));
    }

    // Settings the schema rejects in JSON can still be set in code. They behave as in Java: the direction is
    // compared without regard to case and anything but LEADING keeps the trailing end, and fewer than one
    // character to leave is one.
    [Theory]
    [InlineData(4, "trailing", "Card ************1111 on file.")]
    [InlineData(4, "MIDDLE", "Card ************1111 on file.")]
    [InlineData(4, "leading", "Card 4111************ on file.")]
    [InlineData(0, "LEADING", "Card 4*************** on file.")]
    public void SettingsMadeInCode_BehaveAsInJava(int leave, string direction, string expected)
    {
        var policy = new PhileasPolicy
        {
            Name = "p",
            Identifiers = new Identifiers
            {
                CreditCard = new CreditCard
                {
                    Strategies = new List<CreditCardFilterStrategy>
                    {
                        new() { Strategy = "TRUNCATE", TruncateLeaveCharacters = leave, TruncateDirection = direction }
                    }
                }
            }
        };

        Assert.Equal(expected, Filter(policy, Card));
    }

    [Fact]
    public void PhEyeStrategies_CarryTheTruncateSettings()
    {
        // PhEye strategies are built field by field rather than copied by name, so a setting added to the
        // policy model has to be added there too. Checked here for the settings that copy carries.
        var policyStrategy = new PhEyeFilterStrategy
        {
            Strategy = "TRUNCATE",
            TruncateLeaveCharacters = 3,
            TruncateDirection = "TRAILING",
            TruncateCharacter = "#",
            MaskCharacter = "x",
            MaskLength = "5",
            RedactionFormat = "[%t]",
            StaticReplacement = "static",
            Condition = "confidence > 0.1",
            Color = "red",
            Salt = true
        };
        var policy = new PhileasPolicy
        {
            Name = "p",
            Identifiers = new Identifiers
            {
                PhEyes = new List<PhEye>
                {
                    new()
                    {
                        PhEyeConfiguration = new PhEyeConfiguration { Endpoint = "http://localhost:1" },
                        Strategies = new List<PhEyeFilterStrategy> { policyStrategy }
                    }
                }
            }
        };

        var build = typeof(FilterService).GetMethod("BuildFilters", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var filters = (IEnumerable<Phileas.Filters.AbstractFilter>)build.Invoke(new FilterService(),
            new object[] { policy, new InMemoryContextService() })!;
        var runtime = Assert.Single(Assert.Single(filters.OfType<PhEyeFilter>()).GetStrategies());

        foreach (var name in new[]
                 {
                     "Strategy", "TruncateLeaveCharacters", "TruncateDirection", "TruncateCharacter", "MaskCharacter",
                     "MaskLength", "RedactionFormat", "StaticReplacement", "Condition", "Color", "Salt"
                 })
        {
            Assert.True(Equals(typeof(PhEyeFilterStrategy).GetProperty(name)!.GetValue(policyStrategy),
                    typeof(RuntimeStrategy).GetProperty(name)!.GetValue(runtime)),
                $"{name} was not carried to the PhEye strategy");
        }
    }
}
