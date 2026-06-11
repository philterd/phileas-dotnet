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

using System.Text.RegularExpressions;
using Phileas.Data;
using Phileas.Data.Generators;
using Xunit;

namespace Phileas.Tests.Data;

public class CsvGeneratorTests
{
    [Fact]
    public void GeneratesCsv()
    {
        var dg = new DefaultDataGenerator();
        var csv = new CsvGenerator()
            .AddColumn("First Name", dg.FirstNames())
            .AddColumn("Last Name", dg.Surnames())
            .AddColumn("Age", dg.Age());
        var writer = new StringWriter();
        csv.Generate(writer, 5);
        var lines = writer.ToString().TrimEnd('\n').Split('\n');
        Assert.Equal(6, lines.Length);
        Assert.Equal("First Name,Last Name,Age", lines[0].Trim());
        for (var i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split(',');
            Assert.Equal(3, cols.Length);
            Assert.True(int.Parse(cols[2].Trim()) >= 0);
        }
    }

    [Fact]
    public void EscapesValuesWithDelimiter()
    {
        var commaGen = new ConstGen("Doe, John");
        var csv = new CsvGenerator().AddColumn("Name", commaGen);
        var writer = new StringWriter();
        csv.Generate(writer, 1);
        Assert.Contains("\"Doe, John\"", writer.ToString());
    }

    private sealed class ConstGen : IGenerator<string>
    {
        private readonly string _value;
        public ConstGen(string value) => _value = value;
        public string Random() => _value;
        public long PoolSize() => 1;
    }
}
