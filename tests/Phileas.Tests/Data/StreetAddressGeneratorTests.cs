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

using Phileas.Data.Generators;
using Xunit;

namespace Phileas.Tests.Data;

public class StreetAddressGeneratorTests
{
    private static StreetAddressGenerator Create() =>
        new(new SurnameGenerator(new List<string> { "Smith", "Jones" }, new Random()), new Random());

    [Fact]
    public void GeneratesAddressFormat()
    {
        var value = Create().Random();
        Assert.NotNull(value);
        // <house number> <street name> <suffix>
        Assert.Matches(@"^\d{1,4} \S+ (St|Ave|Blvd|Rd|Ln|Dr|Ct|Pl|Way|Ter)$", value);
    }

    [Fact]
    public void PoolSizeIsPositive()
    {
        Assert.True(Create().PoolSize() > 0);
    }
}
