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

using Phileas.Policy;
using Xunit;

namespace Phileas.Tests;

public class FpeTests
{
    [Fact]
    public void PlainKeyAndTweak()
    {
        var fpe = new Fpe("mykey", "myiv");
        Assert.Equal("mykey", fpe.GetKey());
        Assert.Equal("myiv", fpe.GetTweak());
    }

    [Fact]
    public void KeyResolvesFromEnvironment()
    {
        const string name = "phileas_fpe_key_test";
        Environment.SetEnvironmentVariable(name, "value");
        try
        {
            var fpe = new Fpe("env:" + name, "myiv");
            Assert.Equal("value", fpe.GetKey());
            Assert.Equal("myiv", fpe.GetTweak());
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void TweakResolvesFromEnvironment()
    {
        const string name = "phileas_fpe_tweak_test";
        Environment.SetEnvironmentVariable(name, "value");
        try
        {
            var fpe = new Fpe("mykey", "env:" + name);
            Assert.Equal("mykey", fpe.GetKey());
            Assert.Equal("value", fpe.GetTweak());
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }
}
