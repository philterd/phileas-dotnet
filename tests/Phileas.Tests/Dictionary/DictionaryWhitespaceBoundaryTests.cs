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

using Phileas.Policy.Filters;
using Phileas.Services;
using Xunit;
using PolicyIdentifiers = Phileas.Policy.Identifiers;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests.Dictionaries;

/// <summary>
///     Line breaks and tabs separate words the same way a space does for the word-list filters.
///     See philterd/phileas-dotnet#150.
/// </summary>
public class DictionaryWhitespaceBoundaryTests
{
    private static PolicyIdentifiers Identifiers(string filter, bool fuzzy) => filter switch
    {
        "FirstName" => new PolicyIdentifiers { FirstName = new FirstName { Fuzzy = fuzzy } },
        "Surname" => new PolicyIdentifiers { Surname = new Surname { Fuzzy = fuzzy } },
        "City" => new PolicyIdentifiers { City = new City { Fuzzy = fuzzy } },
        "State" => new PolicyIdentifiers { State = new State { Fuzzy = fuzzy } },
        "County" => new PolicyIdentifiers { County = new County { Fuzzy = fuzzy } },
        "Hospital" => new PolicyIdentifiers { Hospital = new Hospital { Fuzzy = fuzzy } },
        "CustomDictionary" => new PolicyIdentifiers
        {
            CustomDictionaries = new List<CustomDictionary>
            {
                new() { Terms = new List<string> { "Zephyrous" }, Fuzzy = fuzzy }
            }
        },
        // The legacy "dictionary" key, folded into customDictionaries on load (philterd/phileas-dotnet#88).
        // It is only reachable through JSON (philterd/phileas-dotnet#158).
        "Dictionaries" => Phileas.Policy.PolicySerializer
            .DeserializeFromJson("{\"identifiers\":{\"dictionary\":[{\"terms\":[\"Wanderlust\"]}]}}")
            .Identifiers,
        _ => throw new ArgumentException(filter)
    };

    public static IEnumerable<object[]> Cases()
    {
        var terms = new (string Filter, string Before, string Term, string After)[]
        {
            ("FirstName", "Sincerely,", "John", "(2026)"),
            ("Surname", "Mr.", "Jones", "(2026)."),
            ("City", "Moved to", "Boston", "(2026)."),
            ("State", "Moved to", "California", "(2026)."),
            ("County", "He lived in", "Los Angeles", "(2026)."),
            ("Hospital", "Admitted to", "UCLA Medical Center", "(2026)."),
            ("CustomDictionary", "Codename", "Zephyrous", "(2026)."),
            ("Dictionaries", "Project", "Wanderlust", "(2026).")
        };
        var separators = new[] { " ", "\n", "\r\n", "\r", "\t", " " };

        foreach (var (filter, before, term, after) in terms)
        foreach (var separator in separators)
        foreach (var fuzzy in new[] { false, true })
        {
            yield return new object[] { filter, before + separator, term, " " + after, fuzzy };
            yield return new object[] { filter, before + " ", term, separator + after, fuzzy };
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Term_NextToWhitespace_IsRedactedWithExactOffsets(string filter, string prefix, string term,
        string suffix, bool fuzzy)
    {
        var policy = new PhileasPolicy { Name = "p", Identifiers = Identifiers(filter, fuzzy) };
        var input = prefix + term + suffix;

        var result = new FilterService().Filter(policy, "ctx", 0, input);

        Assert.Contains(result.Spans,
            s => s.CharacterStart == prefix.Length && s.CharacterEnd == prefix.Length + term.Length && s.Text == term);
        Assert.DoesNotContain(term, result.FilteredText);
        Assert.StartsWith(prefix, result.FilteredText);
        Assert.EndsWith(suffix, result.FilteredText);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\t")]
    public void BothWordsAroundABreak_AreRedacted(string separator)
    {
        var input = "Patient George" + separator + "Washington was seen";
        var georgeStart = "Patient ".Length;
        var washingtonStart = georgeStart + "George".Length + separator.Length;

        var firstNames = new FilterService().Filter(
            new PhileasPolicy { Name = "p", Identifiers = new PolicyIdentifiers { FirstName = new FirstName() } },
            "ctx", 0, input);
        Assert.Contains(firstNames.Spans, s => s.CharacterStart == georgeStart && s.Text == "George");
        Assert.Contains(firstNames.Spans, s => s.CharacterStart == washingtonStart && s.Text == "Washington");

        var cities = new FilterService().Filter(
            new PhileasPolicy { Name = "p", Identifiers = new PolicyIdentifiers { City = new City() } },
            "ctx", 0, input);
        Assert.Contains(cities.Spans, s => s.CharacterStart == washingtonStart && s.Text == "Washington");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MultiWordTerm_BrokenAcrossALine_IsRedactedAsOneSpan(bool fuzzy)
    {
        const string input = "Admitted to UCLA Medical\r\nCenter today.";
        var policy = new PhileasPolicy
            { Name = "p", Identifiers = new PolicyIdentifiers { Hospital = new Hospital { Fuzzy = fuzzy } } };

        var result = new FilterService().Filter(policy, "ctx", 0, input);

        Assert.Contains(result.Spans,
            s => s.CharacterStart == 12 && s.CharacterEnd == 12 + "UCLA Medical\r\nCenter".Length);
        Assert.Equal("Admitted to {{{REDACTED-hospital}}} today.", result.FilteredText);
    }

    [Theory]
    [InlineData(false, "UCLA Medical Center")]
    [InlineData(true, "UCLA Medical Center")]
    [InlineData(false, "ucla medical center")]
    [InlineData(false, "UCLA\tMedical Center")]
    public void IgnoredTerm_StillIgnored_WhenTheMatchSpansALineBreak(bool fuzzy, string ignoredTerm)
    {
        const string input = "Admitted to UCLA Medical\r\nCenter today.";
        var hospital = new Hospital { Fuzzy = fuzzy, Ignored = new List<string> { ignoredTerm } };
        var policy = new PhileasPolicy { Name = "p", Identifiers = new PolicyIdentifiers { Hospital = hospital } };

        var result = new FilterService().Filter(policy, "ctx", 0, input);

        Assert.Equal(input, result.FilteredText);
    }

    [Fact]
    public void RunOfSpaces_StillSeparatesWords()
    {
        var policy = new PhileasPolicy
            { Name = "p", Identifiers = new PolicyIdentifiers { FirstName = new FirstName() } };

        var result = new FilterService().Filter(policy, "ctx", 0, "Dear   John,");

        Assert.Equal("Dear   {{{REDACTED-first-name}}},", result.FilteredText);
    }
}
