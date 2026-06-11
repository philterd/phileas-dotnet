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

public class TrackingNumberGeneratorTests
{
    [Fact]
    public void GeneratesTracking()
    {
        var t = new TrackingNumberGenerator(new Random()).Random();
        Assert.True(t.StartsWith("1Z") || Regex.IsMatch(t, "^\\d{12}$"), t);
    }

    [Fact]
    public void PoolSize()
    {
        Assert.Equal(long.MaxValue, new TrackingNumberGenerator(new Random()).PoolSize());
    }
}
