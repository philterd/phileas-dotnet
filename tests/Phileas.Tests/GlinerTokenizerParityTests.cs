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

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.ML.Tokenizers;
using Xunit;

namespace Phileas.Tests;

/// <summary>
///     Token-for-token parity between the .NET <see cref="SentencePieceTokenizer" /> and the Python reference for the
///     deberta-v3 SentencePiece model that the local GLiNER path depends on. Unlike the gated end-to-end
///     <see cref="GlinerModelTests" />, this runs in default CI: it needs only the committed <c>spm.model</c> fixture
///     (about 2.4 MB), no ONNX graph and no environment variable. The model-free <see cref="GlinerPipelineTests" />
///     deliberately use a fake encoder, so without this test a regression in the real tokenizer integration -- a package
///     bump that changed normalization, the leading-space marker, or sub-word merges -- would slip through a normal run.
///     The golden ids in <c>Resources/Gliner/tokenizer_parity.json</c> were produced by the Python <c>sentencepiece</c>
///     library from the same <c>spm.model</c>; see <c>generate_tokenizer_fixture.py</c> for the generator and inputs.
/// </summary>
public class GlinerTokenizerParityTests
{
    // <<ENT>> / <<SEP>> are GLiNER sentinels, not SentencePiece vocabulary; GlinerModel registers them as specials
    // with these reserved deberta-v3 ids (class_token_index and the next id). Mirror that construction here.
    private const int EntId = 128002;
    private const int SepId = 128003;

    private static readonly string GlinerAssets = Path.Combine(AppContext.BaseDirectory, "Resources", "Gliner");

    private static SentencePieceTokenizer CreateTokenizer()
    {
        using var spmStream = File.OpenRead(Path.Combine(GlinerAssets, "spm.model"));
        return SentencePieceTokenizer.Create(spmStream, false, false,
            new Dictionary<string, int> { { "<<ENT>>", EntId }, { "<<SEP>>", SepId } });
    }

    public static IEnumerable<object[]> FixtureCases()
    {
        var json = File.ReadAllText(Path.Combine(GlinerAssets, "tokenizer_parity.json"));
        var fixture = JsonSerializer.Deserialize<Fixture>(json)
                      ?? throw new InvalidOperationException("tokenizer_parity.json failed to deserialize.");
        foreach (var c in fixture.Cases)
            yield return new object[] { c.Input, c.Ids };
    }

    [Theory]
    [MemberData(nameof(FixtureCases))]
    public void EncodeToIds_MatchesPythonReference(string input, int[] expectedIds)
    {
        var tokenizer = CreateTokenizer();

        var ids = tokenizer.EncodeToIds(input).ToArray();

        // Id-for-id: any divergence in normalization, the leading-space marker, or sub-word merges fails here.
        Assert.Equal(expectedIds, ids);
    }

    [Fact]
    public void Create_RegistersGlinerSentinelsAsSpecials()
    {
        var tokenizer = CreateTokenizer();

        // The pipeline relies on these mapping to their reserved ids as whole tokens; they are not in spm.model, so
        // this guards the special-token registration the fixture cases (ordinary text) cannot exercise.
        Assert.Equal(new[] { EntId }, tokenizer.EncodeToIds("<<ENT>>").ToArray());
        Assert.Equal(new[] { SepId }, tokenizer.EncodeToIds("<<SEP>>").ToArray());
    }

    private sealed record Fixture(
        [property: JsonPropertyName("cases")] IReadOnlyList<FixtureCase> Cases);

    private sealed record FixtureCase(
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("ids")] int[] Ids);
}
