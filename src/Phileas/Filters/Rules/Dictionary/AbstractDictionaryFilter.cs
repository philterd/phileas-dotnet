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
using System.Text.RegularExpressions;
using Rx = System.Text.RegularExpressions.Regex;
using Phileas.Filters.Rules;
using Phileas.Model;

namespace Phileas.Filters.Rules.Dictionary;

/// <summary>
///     Base class for dictionary-backed filters. Loads the term pool for a name/location
///     <see cref="FilterType" /> from the embedded resource files (the .NET analog of the Java classpath
///     resources), compiling each term into a word-boundary regex.
/// </summary>
public abstract class AbstractDictionaryFilter : RulesFilter
{
    /// <summary>Initializes the dictionary filter.</summary>
    protected AbstractDictionaryFilter(FilterType filterType, FilterConfiguration configuration)
        : base(filterType, configuration)
    {
    }

    /// <summary>Loads the dictionary for a built-in name/location <paramref name="filterType" />.</summary>
    /// <exception cref="ArgumentException">If the filter type has no associated dictionary.</exception>
    protected Dictionary<string, Rx> LoadData(FilterType filterType)
    {
        var fileName = filterType switch
        {
            FilterType.LocationCity => "cities.txt",
            FilterType.LocationCounty => "counties.txt",
            FilterType.LocationState => "states.txt",
            FilterType.Hospital => "hospitals.txt",
            FilterType.FirstName => "first-names.txt",
            FilterType.Surname => "surnames.txt",
            _ => throw new ArgumentException("Invalid filter type.", nameof(filterType))
        };

        return LoadData(ReadResourceLines(fileName));
    }

    /// <summary>Builds a dictionary from an explicit set of <paramref name="terms" />.</summary>
    protected Dictionary<string, Rx> LoadData(IEnumerable<string> terms)
    {
        var dictionary = new Dictionary<string, Rx>();
        foreach (var term in terms)
        {
            if (string.IsNullOrWhiteSpace(term)) continue;
            dictionary[term] = new Rx(@"\b" + Rx.Escape(term) + @"\b", RegexOptions.IgnoreCase);
        }

        return dictionary;
    }

    private static IEnumerable<string> ReadResourceLines(string fileName)
    {
        var assembly = typeof(AbstractDictionaryFilter).Assembly;
        var manifestName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("." + fileName, StringComparison.Ordinal));

        if (manifestName == null)
        {
            throw new IOException("Resource not found: " + fileName);
        }

        using var stream = assembly.GetManifestResourceStream(manifestName)!;
        using var reader = new StreamReader(stream);

        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line.Trim());
            }
        }

        return lines;
    }
}
