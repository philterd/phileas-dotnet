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
using Phileas.Services;
using Xunit;

namespace Phileas.Tests.Dictionaries;

/// <summary>
///     A filter strategy behaves the same on a dictionary filter as on a regex one. The two were built
///     by separate code paths, and the dictionary path hand-copied eleven properties and skipped the
///     policy's crypto settings. See philterd/phileas-dotnet#125.
/// </summary>
public class DictionaryStrategyParityTests
{
    private const string CryptoBlock = "\"crypto\":{\"key\":\"000102030405060708090a0b0c0d0e0f\"},";
    private const string FpeBlock =
        "\"fpe\":{\"key\":\"000102030405060708090a0b0c0d0e0f\",\"tweak\":\"0011223344556677\"},";

    // The same literal is detected as an SSN by a regex filter and as a term by a dictionary filter,
    // so the two construction paths can be compared on identical input.
    private const string Value = "078-05-1120";

    private static string Filtered(string json, string input)
    {
        return new FilterService().Filter(PolicySerializer.DeserializeFromJson(json), "ctx", 0, input)
            .FilteredText;
    }

    /// <summary>The same value and strategy through a dictionary filter.</summary>
    private static string ThroughDictionary(string strategy, string extra = "", string top = "")
    {
        return Filtered("{" + top + "\"identifiers\":{\"dictionaries\":[{\"terms\":[\"" + Value + "\"],"
                        + "\"customFilterStrategies\":[{\"strategy\":\"" + strategy + "\"" + extra + "}]}]}}",
            Value);
    }

    /// <summary>
    ///     The same value and strategy through a regex filter. <c>ssn</c> deliberately: the custom
    ///     <c>identifiers</c> filter is built by the dictionary path too, so comparing against it would
    ///     compare that path with itself and prove nothing.
    /// </summary>
    private static string ThroughRegex(string strategy, string extra = "", string top = "")
    {
        return Filtered("{" + top + "\"identifiers\":{\"ssn\":{\"ssnFilterStrategies\":[{\"strategy\":\""
                        + strategy + "\"" + extra + "}]}}}", Value);
    }

    // ---------------- MAP_REPLACE ----------------

    [Fact]
    public void MapReplaceUsesItsLookupTableOnADictionaryFilter()
    {
        // The table never reached the runtime strategy, so the strategy always fell through to its
        // fallback and the mapping silently did nothing.
        Assert.Equal("MAPPED", ThroughDictionary("MAP_REPLACE", ",\"mappings\":{\"" + Value + "\":\"MAPPED\"}"));
    }

    [Fact]
    public void MapReplaceFallsBackWhenTheValueIsNotInTheTable()
    {
        // A miss must still remove the value: nothing is left in the clear.
        var filtered = ThroughDictionary("MAP_REPLACE", ",\"mappings\":{\"other\":\"MAPPED\"}");

        Assert.DoesNotContain(Value, filtered);
        Assert.Contains("REDACTED", filtered);
    }

    [Fact]
    public void MapReplaceHonoursItsFallbackStrategy()
    {
        Assert.Equal("1120",
            ThroughDictionary("MAP_REPLACE", ",\"mappings\":{},\"fallbackStrategy\":\"LAST_4\""));
    }

    [Theory]
    [InlineData("true", "REDACTED")] // the key differs in case, so the lookup misses
    [InlineData("false", "MAPPED")]
    public void MapReplaceHonoursCaseSensitivity(string caseSensitive, string expected)
    {
        var filtered = Filtered("{\"identifiers\":{\"dictionaries\":[{\"terms\":[\"Smith\"],"
                                + "\"customFilterStrategies\":[{\"strategy\":\"MAP_REPLACE\","
                                + "\"mappings\":{\"SMITH\":\"MAPPED\"},\"caseSensitive\":" + caseSensitive
                                + "}]}]}}", "Smith");

        Assert.Contains(expected, filtered);
    }

    // ---------------- the policy's crypto settings ----------------

    [Fact]
    public void CryptoReplaceNoLongerFailsOnADictionaryFilter()
    {
        // The dictionary path never passed the policy's crypto block to the filter configuration, so
        // building the filter threw "Missing crypto encryption property" against a policy that had one.
        var filtered = ThroughDictionary("CRYPTO_REPLACE", top: CryptoBlock);

        Assert.DoesNotContain(Value, filtered);
        Assert.StartsWith("{{", filtered);
    }

    [Fact]
    public void FpeEncryptReplacePreservesTheFormatOnADictionaryFilter()
    {
        var filtered = ThroughDictionary("FPE_ENCRYPT_REPLACE", top: FpeBlock);

        // Format-preserving: the digits change, the length and the punctuation positions do not.
        Assert.NotEqual(Value, filtered);
        Assert.Equal(Value.Length, filtered.Length);
        for (var i = 0; i < Value.Length; i++)
            Assert.Equal(char.IsDigit(Value[i]), char.IsDigit(filtered[i]));
    }

    // ---------------- the two paths agree ----------------

    [Theory]
    [InlineData("REDACT", "", "")]
    [InlineData("MASK", "", "")]
    [InlineData("LAST_4", "", "")]
    [InlineData("TRUNCATE", "", "")]
    [InlineData("HASH_SHA256_REPLACE", "", "")]
    [InlineData("STATIC_REPLACE", ",\"staticReplacement\":\"X\"", "")]
    [InlineData("MAP_REPLACE", ",\"mappings\":{\"078-05-1120\":\"MAPPED\"}", "")]
    [InlineData("FPE_ENCRYPT_REPLACE", "", FpeBlock)]
    public void AStrategyBehavesTheSameOnEitherKindOfFilter(string strategy, string extra, string top)
    {
        // The redaction label differs by filter type, so REDACT is compared on the shape of the result
        // rather than the text.
        var dictionary = ThroughDictionary(strategy, extra, top);
        var regex = ThroughRegex(strategy, extra, top);

        if (strategy == "REDACT")
        {
            Assert.StartsWith("{{{REDACTED-", dictionary);
            Assert.StartsWith("{{{REDACTED-", regex);
            return;
        }

        Assert.Equal(regex, dictionary);
    }

    [Fact]
    public void CryptoReplaceProducesADifferentCiphertextEachTimeOnBothPaths()
    {
        // AES-GCM uses a fresh nonce per call, so the two paths cannot be compared by value; what must
        // match is that both encrypt rather than one of them falling back to redaction.
        foreach (var filtered in new[]
                 {
                     ThroughDictionary("CRYPTO_REPLACE", top: CryptoBlock),
                     ThroughRegex("CRYPTO_REPLACE", top: CryptoBlock)
                 })
        {
            Assert.StartsWith("{{", filtered);
            Assert.DoesNotContain("REDACTED", filtered);
        }
    }

    [Fact]
    public void AStrategyTheAlphabetCannotEncryptFallsBackOnBothPaths()
    {
        // FF3-1 does not cover a short alphabetic value, and both paths fall back to redaction for it.
        var dictionary = Filtered("{" + FpeBlock + "\"identifiers\":{\"dictionaries\":[{\"terms\":[\"Smith\"],"
                                  + "\"customFilterStrategies\":[{\"strategy\":\"FPE_ENCRYPT_REPLACE\"}]}]}}",
            "Smith");
        var regex = Filtered("{" + FpeBlock + "\"identifiers\":{\"surname\":{\"surnameFilterStrategies\":"
                             + "[{\"strategy\":\"FPE_ENCRYPT_REPLACE\"}]}}}", "Smith");

        Assert.Contains("REDACTED", dictionary);
        Assert.Contains("REDACTED", regex);
    }

    [Fact]
    public void ABuiltInDictionaryFilterGetsTheSameTreatment()
    {
        // surname and the other bundled dictionaries go through the same path as the custom ones.
        var filtered = Filtered("{\"identifiers\":{\"surname\":{\"surnameFilterStrategies\":"
                                + "[{\"strategy\":\"MAP_REPLACE\",\"mappings\":{\"Smith\":\"MAPPED\"}}]}}}",
            "Smith here");

        Assert.Equal("MAPPED here", filtered);
    }
}
