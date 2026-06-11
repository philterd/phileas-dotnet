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

public class FullNameGeneratorTests
{
    [Fact]
    public void GeneratesTwoParts()
    {
        var first = new FirstNameGenerator(new List<string> { "John", "Jane", "Mary" }, new Random());
        var sur = new SurnameGenerator(new List<string> { "Doe", "Smith", "Jones" }, new Random());
        var gen = new FullNameGenerator(first, sur);
        var name = gen.Random();
        Assert.Contains(" ", name);
        Assert.Equal(2, name.Split(' ').Length);
        Assert.Equal(9, gen.PoolSize());
    }
}
