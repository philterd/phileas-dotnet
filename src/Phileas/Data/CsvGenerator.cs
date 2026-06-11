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

using System.Text;
using Phileas.Data.Generators;

namespace Phileas.Data;

/// <summary>
///     Builds a CSV of synthetic data, one column per registered generator. Mirrors the Java
///     <c>CsvGenerator</c>.
/// </summary>
public class CsvGenerator
{
    private readonly List<(string Name, Func<object?> Value)> _columns = new();
    private string _delimiter = ",";
    private bool _useQuotes;

    /// <summary>Sets the field delimiter (default <c>","</c>).</summary>
    public CsvGenerator WithDelimiter(string delimiter)
    {
        _delimiter = delimiter;
        return this;
    }

    /// <summary>Sets whether every field is quoted (default <see langword="false" />).</summary>
    public CsvGenerator WithQuotes(bool useQuotes)
    {
        _useQuotes = useQuotes;
        return this;
    }

    /// <summary>Adds a column backed by the given generator.</summary>
    public CsvGenerator AddColumn<T>(string name, IGenerator<T> generator)
    {
        _columns.Add((name, () => generator.Random()));
        return this;
    }

    /// <summary>Writes <paramref name="rows" /> data rows (plus a header) to <paramref name="writer" />.</summary>
    public void Generate(TextWriter writer, int rows)
    {
        writer.Write(string.Join(_delimiter, _columns.Select(c => EscapeCsv(c.Name))));
        writer.Write('\n');

        for (var i = 0; i < rows; i++)
        {
            var row = new StringBuilder();
            for (var col = 0; col < _columns.Count; col++)
            {
                row.Append(EscapeCsv(Convert.ToString(_columns[col].Value()) ?? "null"));
                if (col < _columns.Count - 1) row.Append(_delimiter);
            }
            writer.Write(row.ToString());
            writer.Write('\n');
        }

        writer.Flush();
    }

    private string EscapeCsv(string value)
    {
        if (_useQuotes || value.Contains(_delimiter) || value.Contains('"') || value.Contains('\n')
            || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}
