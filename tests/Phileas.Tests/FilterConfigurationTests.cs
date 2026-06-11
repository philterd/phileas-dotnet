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

using Phileas.Filters;
using Phileas.Filters.Strategies.Rules;
using Phileas.Policy;
using Xunit;

namespace Phileas.Tests;

public class FilterConfigurationTests
{
    private const string ValidAes256Key = "9EE7A356FDFE43F069500B0086758346E66D8583E0CE1CFCA04E50F67ECCE5D1";

    private static FilterConfiguration.Builder CryptoBuilderWithKey(string? key)
    {
        var strategy = new SsnFilterStrategy { Strategy = "CRYPTO_REPLACE" };
        return new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy> { strategy })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .WithWindowSize(5)
            .WithCrypto(new Crypto(key, null));
    }

    [Fact]
    public void ValidCryptoKeyPasses()
    {
        var exception = Record.Exception(() => CryptoBuilderWithKey(ValidAes256Key).Build());
        Assert.Null(exception);
    }

    [Fact]
    public void NonHexCryptoKeyIsRejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CryptoBuilderWithKey("not-hexadecimal!").Build());
        Assert.Contains("hexadecimal", ex.Message);
    }

    [Fact]
    public void WrongLengthCryptoKeyIsRejected()
    {
        // "ABCD" decodes to two bytes - not a legal AES key length.
        var ex = Assert.Throws<InvalidOperationException>(() => CryptoBuilderWithKey("ABCD").Build());
        Assert.Contains("AES key", ex.Message);
    }

    [Fact]
    public void MissingCryptoKeyIsRejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CryptoBuilderWithKey(null).Build());
        Assert.Contains("Missing crypto encryption key", ex.Message);
    }
}
