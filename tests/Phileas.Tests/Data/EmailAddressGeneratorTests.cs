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

public class EmailAddressGeneratorTests
{
    [Fact]
    public void GeneratesEmail()
    {
        var first = new FirstNameGenerator(new List<string> { "John", "Jane", "Mary" }, new Random());
        var sur = new SurnameGenerator(new List<string> { "Doe", "Smith", "Jones" }, new Random());
        var gen = new EmailAddressGenerator(first, sur, new Random());
        var email = gen.Random();
        Assert.Contains("@", email);
        Assert.Contains(".", email);
        Assert.Matches(".*\\d{3}", email.Split('@')[0]);
    }

    [Fact]
    public void CustomDomain()
    {
        var first = new FirstNameGenerator(new List<string> { "John" }, new Random());
        var sur = new SurnameGenerator(new List<string> { "Doe" }, new Random());
        var gen = new EmailAddressGenerator(first, sur, new Random(), new[] { "test.com" });
        Assert.EndsWith("@test.com", gen.Random());
    }

    [Fact]
    public void PoolSize()
    {
        var first = new FirstNameGenerator(new List<string> { "John", "Jane", "Mary" }, new Random());
        var sur = new SurnameGenerator(new List<string> { "Doe", "Smith", "Jones" }, new Random());
        var gen = new EmailAddressGenerator(first, sur, new Random(), new[] { "a.com", "b.com" });
        Assert.Equal(3 * 3 * 1000L * 2, gen.PoolSize());
    }

    [Fact]
    public void DefaultConstructorLoadsResources()
    {
        var gen = new EmailAddressGenerator(new Random());
        Assert.Contains("@", gen.Random());
    }
}
