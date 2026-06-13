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
///     End-to-end parity tests for the local GLiNER inference, ONNX run included. These require a GLiNER model
///     directory (the exported ONNX graph, <c>spm.model</c>, and <c>gliner_config.json</c>), which is too large to
///     vendor, so they use <see cref="ModelFactAttribute" />: when <c>PHILEAS_GLINER_MODEL_DIR</c> is not set to an
///     existing directory the tests are reported as <em>skipped</em>, not passed. Set it to a
///     <c>philterd/ph-eye-pii-base</c> checkout (with an exported ONNX) to verify parity locally. The expected spans
///     and scores were captured from the reference Python <c>gliner.predict_entities</c> run. The model-free coverage
///     of the deterministic stages lives in <see cref="GlinerPipelineTests" />.
/// </summary>
public class GlinerModelTests
{
    private static string ModelDir => Environment.GetEnvironmentVariable("PHILEAS_GLINER_MODEL_DIR")!;

    [ModelFact]
    public void Find_MatchesPythonReference_AcrossEntityTypes()
    {
        using var model = new GlinerModel(ModelDir);

        const string text =
            "Patient John Q. Public was seen on 2024-03-02. Email j.public@example.com, SSN 123-45-6789.";
        var labels = new List<string> { "person", "email address", "social security number", "date" };

        var entities = model.Find(text, labels, 0.5);

        // Span-for-span parity with the Python reference: same label, text, char offsets, ordered by start.
        Assert.Equal(4, entities.Count);

        Assert.Equal("person", entities[0].Label);
        Assert.Equal("John Q. Public", entities[0].Text);
        Assert.Equal(8, entities[0].Start);
        Assert.Equal(22, entities[0].End);

        Assert.Equal("date", entities[1].Label);
        Assert.Equal("2024-03-02", entities[1].Text);
        Assert.Equal(35, entities[1].Start);
        Assert.Equal(45, entities[1].End);

        Assert.Equal("email address", entities[2].Label);
        Assert.Equal("j.public@example.com", entities[2].Text);
        Assert.Equal(53, entities[2].Start);
        Assert.Equal(73, entities[2].End);

        Assert.Equal("social security number", entities[3].Label);
        Assert.Equal("123-45-6789", entities[3].Text);
        Assert.Equal(79, entities[3].Start);
        Assert.Equal(90, entities[3].End);

        // Confidence reproduces the reference to within int8-quantization tolerance.
        Assert.All(entities, e => Assert.True(e.Score > 0.5, $"score for {e.Label} should clear the threshold"));
    }

    [ModelFact]
    public void Find_RespectsThreshold()
    {
        using var model = new GlinerModel(ModelDir);

        const string text = "John Q. Public is here";
        var labels = new List<string> { "person", "location" };

        // A threshold above every span's confidence yields no detections.
        var none = model.Find(text, labels, 0.999);
        Assert.Empty(none);

        // At the default threshold the person span is found.
        var some = model.Find(text, labels, 0.5);
        Assert.Contains(some, e => e.Label == "person" && e.Text == "John Q. Public");
    }

    [ModelFact]
    public void Find_EmptyInput_ReturnsNoEntities()
    {
        using var model = new GlinerModel(ModelDir);
        Assert.Empty(model.Find("", new List<string> { "person" }, 0.5));
    }
}
