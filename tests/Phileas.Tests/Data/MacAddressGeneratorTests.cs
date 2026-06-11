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

public class MacAddressGeneratorTests
{
    [Fact]
    public void GeneratesValidFormat()
    {
        var value = new MacAddressGenerator(new Random()).Random();
        Assert.NotNull(value);
        Assert.Matches(@"^([0-9A-F]{2}:){5}[0-9A-F]{2}$", value);
    }
    [Fact]
    public void PoolSize()
    {
        Assert.Equal(281474976710656L, new MacAddressGenerator(new Random()).PoolSize());
    }
}
