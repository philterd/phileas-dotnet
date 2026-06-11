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

public class AbstractGeneratorTests
{
    private sealed class TestGenerator : AbstractGenerator<string>
    {
        public override string Random() => string.Empty;
        public override long PoolSize() => 0;
        public List<string> TestLoadNames(string path) => LoadNames(path);
    }

    [Fact]
    public void LoadNamesReadsResource()
    {
        var names = new TestGenerator().TestLoadNames("/first-names.txt");
        Assert.NotEmpty(names);
        Assert.All(names, n => Assert.False(string.IsNullOrWhiteSpace(n)));
    }

    [Fact]
    public void LoadNamesThrowsWhenMissing()
    {
        Assert.Throws<IOException>(() => new TestGenerator().TestLoadNames("/non-existent.txt"));
    }
}
