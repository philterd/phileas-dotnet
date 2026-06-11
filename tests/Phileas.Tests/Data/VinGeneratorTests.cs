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

public class VinGeneratorTests
{
    [Fact]
    public void GeneratesValidFormat()
    {
        var value = new VinGenerator(new Random()).Random();
        Assert.NotNull(value);
        Assert.Matches(@"^[A-HJ-NPR-Z0-9]{17}$", value);
    }
    [Fact]
    public void PoolSize()
    {
        Assert.Equal(long.MaxValue, new VinGenerator(new Random()).PoolSize());
    }
}
