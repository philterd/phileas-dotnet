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

public class CustomIdGeneratorTests
{
    [Fact]
    public void GeneratesMatchingPattern()
    {
        var id = new CustomIdGenerator(new Random(), "123-ABC-abc").Random();
        Assert.Equal(11, id!.Length);
        Assert.True(char.IsDigit(id[0]) && char.IsDigit(id[1]) && char.IsDigit(id[2]));
        Assert.Equal('-', id[3]);
        Assert.True(char.IsUpper(id[4]) && char.IsUpper(id[5]) && char.IsUpper(id[6]));
        Assert.Equal('-', id[7]);
        Assert.True(char.IsLower(id[8]) && char.IsLower(id[9]) && char.IsLower(id[10]));
    }

    [Fact]
    public void PoolSizes()
    {
        Assert.Equal(1000L, new CustomIdGenerator(new Random(), "123").PoolSize());
        Assert.Equal(26L * 26L * 26L, new CustomIdGenerator(new Random(), "ABC").PoolSize());
        Assert.Equal(10L * 26L, new CustomIdGenerator(new Random(), "1A-").PoolSize());
        Assert.Equal(1L, new CustomIdGenerator(new Random(), "").PoolSize());
        Assert.Equal(1L, new CustomIdGenerator(new Random(), "!!!").PoolSize());
    }

    [Fact]
    public void NullPattern()
    {
        var gen = new CustomIdGenerator(new Random(), null);
        Assert.Null(gen.Random());
        Assert.Equal(0, gen.PoolSize());
    }

    [Fact]
    public void EmptyPattern()
    {
        var gen = new CustomIdGenerator(new Random(), "");
        Assert.Equal("", gen.Random());
        Assert.Equal(1, gen.PoolSize());
    }

    [Fact]
    public void SpecialCharactersPreserved()
    {
        var gen = new CustomIdGenerator(new Random(), "!@#");
        Assert.Equal("!@#", gen.Random());
    }

    [Fact]
    public void SingleArgConstructor()
    {
        var gen = new CustomIdGenerator("123");
        Assert.NotNull(gen.Random());
        Assert.Equal(1000L, gen.PoolSize());
    }
}
