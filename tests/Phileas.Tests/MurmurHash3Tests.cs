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
using Phileas.Utils;
using Xunit;

namespace Phileas.Tests;

public class MurmurHash3Tests
{
    // Canonical MurmurHash3 x86_32 (seed 0) reference values, matching Apache commons-codec's
    // hash32x86(byte[]). "The quick brown fox..." -> 0x2e4ff723 is the widely-published reference vector.
    [Theory]
    [InlineData("", 0)]
    [InlineData("test", -1167338989)]
    [InlineData("The quick brown fox jumps over the lazy dog", 776992547)]
    [InlineData("naïve", 992511445)]
    public void Hash32X86_MatchesCanonicalReferenceValues(string input, int expected)
    {
        Assert.Equal(expected, MurmurHash3.Hash32X86(Encoding.UTF8.GetBytes(input)));
    }
}
