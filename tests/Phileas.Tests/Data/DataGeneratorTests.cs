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

public class DataGeneratorTests
{
    private readonly DefaultDataGenerator _generator = new();

    [Fact]
    public void PoolSizes()
    {
        Assert.True(_generator.FirstNames().PoolSize() > 0);
        Assert.True(_generator.Surnames().PoolSize() > 0);
        Assert.True(_generator.FullNames().PoolSize() > 0);
        Assert.Equal(900000000L, _generator.Ssn().PoolSize());
        Assert.Equal(8100000000L, _generator.PhoneNumbers().PoolSize());
        Assert.True(_generator.EmailAddresses().PoolSize() > 0);
        Assert.Equal(101L, _generator.Age().PoolSize());
        Assert.Equal(1000000000L, _generator.BankRoutingNumbers().PoolSize());
        Assert.Equal(10000L * 10000L * 10000L * 10000L, _generator.CreditCardNumbers().PoolSize());
        Assert.True(_generator.Dates().PoolSize() >= 60L * 365L);
        Assert.Equal(long.MaxValue, _generator.Iban().PoolSize());
        Assert.Equal(4294967296L, _generator.IpAddresses().PoolSize());
        Assert.Equal(281474976710656L, _generator.MacAddresses().PoolSize());
        Assert.Equal(2600000000L, _generator.PassportNumbers().PoolSize());
        Assert.Equal(50L, _generator.States().PoolSize());
        Assert.Equal(50L, _generator.StateAbbreviations().PoolSize());
        Assert.Equal(100000L, _generator.ZipCodes().PoolSize());
        Assert.Equal(long.MaxValue, _generator.BitcoinAddresses().PoolSize());
        Assert.Equal(long.MaxValue, _generator.Vin().PoolSize());
        Assert.True(_generator.Urls().PoolSize() > 0);
        Assert.True(_generator.Hospitals().PoolSize() > 0);
        Assert.Equal(long.MaxValue, _generator.TrackingNumbers().PoolSize());
        Assert.True(_generator.StreetAddresses().PoolSize() > 0);
        Assert.Equal(18720L, _generator.Cities().PoolSize());
        Assert.True(_generator.Counties().PoolSize() > 0);
        Assert.Equal(1000L, _generator.CustomId("123").PoolSize());
    }

    [Fact]
    public void CustomRandomIsDeterministic()
    {
        var g1 = new DefaultDataGenerator(new Random(12345));
        var g2 = new DefaultDataGenerator(new Random(12345));
        Assert.Equal(g1.Ssn().Random(), g2.Ssn().Random());
        Assert.Equal(g1.FirstNames().Random(), g2.FirstNames().Random());
    }

    [Fact]
    public void FactoryRandomAndPoolSize()
    {
        Assert.Null(_generator.Random());
        Assert.Equal(0, _generator.PoolSize());
    }

    [Fact]
    public void StreetAddressFormat()
    {
        Assert.Matches(@"^\d+ .* (St|Ave|Blvd|Rd|Ln|Dr|Ct|Pl|Way|Ter)$", _generator.StreetAddresses().Random());
    }

    [Fact]
    public void DatesWithPattern()
    {
        Assert.Matches(@"^\d{2}-\d{2}-\d{4}$", _generator.Dates("MM-dd-yyyy").Random());
    }
}
