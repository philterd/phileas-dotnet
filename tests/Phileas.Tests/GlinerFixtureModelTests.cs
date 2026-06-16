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

using Phileas.Filters.PhEye;
using Xunit;

namespace Phileas.Tests;

/// <summary>
///     End-to-end coverage of <see cref="GlinerModel" /> against a tiny synthetic ONNX fixture, run in default CI with
///     a real <c>InferenceSession.Run</c> and the real SentencePiece tokenizer. This closes the gap the gated
///     <see cref="GlinerModelTests" /> leave open: those need the 183 MB / 611 MB production model and are skipped
///     unless <c>PHILEAS_GLINER_MODEL_DIR</c> is set, so the actual ONNX feed/fetch wiring and tokenizer integration
///     are otherwise never exercised by an automated run. The fixture (<c>Resources/Gliner/model.onnx</c>, built by
///     <c>generate_onnx_fixture.py</c>) reproduces the GLiNER markerV0 signature and emits deterministic logits: a
///     firing span at (word 0, width 1 -> words 0..1) plus the lower-scored span it contains (word 0, width 0), so the
///     decode's greedy non-overlapping resolution is exercised through the real run too. It does not carry the real
///     weights, so it guards the C# wiring and decode, not weight parity -- that stays in <see cref="GlinerModelTests" />.
/// </summary>
public class GlinerFixtureModelTests
{
    // The fixture directory is a self-contained GLiNER model: model.onnx + gliner_config.json + spm.model.
    private static string FixtureModelDir =>
        Path.Combine(AppContext.BaseDirectory, "Resources", "Gliner");

    [Fact]
    public void Find_AgainstFixture_DetectsWiderSpanAndResolvesOverlap()
    {
        using var model = new GlinerModel(FixtureModelDir);

        // The fixture fires the two-word span (word 0..1, label 0) at +10 and the single-word span it contains at +6.
        // Greedy non-overlapping decode keeps the wider, higher-scored span and drops the contained one, so a correct
        // run returns exactly one entity spanning both words. This drives the whole path: SplitWords -> BuildInputs ->
        // ONNX Run -> Decode (threshold, span->offset, label selection, and overlap resolution).
        var entities = model.Find("Alice Bob", new List<string> { "person", "location" }, 0.5);

        var entity = Assert.Single(entities);
        Assert.Equal("person", entity.Label);
        Assert.Equal("Alice Bob", entity.Text);
        Assert.Equal(0, entity.Start);
        Assert.Equal(9, entity.End);
        // The firing logit is +10, so sigmoid ~= 0.99995; the score must clear the 0.5 threshold.
        Assert.True(entity.Score > 0.5);
    }

    [Fact]
    public void Find_AgainstFixture_RespectsThreshold()
    {
        using var model = new GlinerModel(FixtureModelDir);

        // sigmoid(+10) ~= 0.99995: above 0.5 (detected) but below 0.99999 (a threshold it cannot clear).
        Assert.Single(model.Find("Alice Bob", new List<string> { "person" }, 0.5));
        Assert.Empty(model.Find("Alice Bob", new List<string> { "person" }, 0.99999));
    }

    [Fact]
    public void Find_AgainstFixture_MapsFiringSpanToTheChosenLabel()
    {
        using var model = new GlinerModel(FixtureModelDir);

        // Label index 0 is the one that fires regardless of the label strings, confirming the words_mask/label
        // alignment carries through the ONNX run: swapping the label order swaps which string is reported.
        var asDate = model.Find("Alice Bob", new List<string> { "date", "person" }, 0.5);
        Assert.Equal("date", Assert.Single(asDate).Label);
    }

    [Fact]
    public void Find_EmptyInput_ReturnsNoEntities()
    {
        using var model = new GlinerModel(FixtureModelDir);
        Assert.Empty(model.Find("", new List<string> { "person" }, 0.5));
    }
}
