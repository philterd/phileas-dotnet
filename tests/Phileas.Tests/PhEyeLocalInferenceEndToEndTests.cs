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

using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

/// <summary>
///     Full-stack coverage of local GLiNER inference driven from a policy: a policy whose <c>pheyes</c> block sets
///     <c>modelPath</c> is run through <see cref="FilterService" />, which builds the <see cref="Phileas.Filters.PhEye.PhEyeFilter" />,
///     takes its on-device (<c>DetectLocal</c>) branch, runs a real ONNX <c>InferenceSession</c>, and the resulting
///     spans are asserted. This is the only test that exercises the whole chain — policy → FilterService → PhEyeFilter
///     local inference → GlinerModel → spans/redaction — and it runs in default CI by pointing <c>modelPath</c> at the
///     committed synthetic GLiNER fixture (no large model, no environment variable). The model-level inference is
///     covered by <see cref="GlinerFixtureModelTests" />; this adds the PhEyeFilter mapping and policy wiring on top.
/// </summary>
public class PhEyeLocalInferenceEndToEndTests
{
    // The committed synthetic GLiNER fixture doubles as a self-contained local model directory
    // (model.onnx + gliner_config.json + spm.model); it fires the two-word span (word 0..1, label 0).
    private static string FixtureModelDir =>
        Path.Combine(AppContext.BaseDirectory, "Resources", "Gliner");

    private static PhileasPolicy LocalModelPersonPolicy()
    {
        return new PhileasPolicy
        {
            Name = "pheye-local-model",
            Identifiers = new Identifiers
            {
                PhEyes = new List<PhEye>
                {
                    new()
                    {
                        // modelPath set => on-device inference; labels become the GLiNER prompt.
                        PhEyeConfiguration = new PhEyeConfiguration
                        {
                            ModelPath = FixtureModelDir,
                            Labels = new List<string> { "person" },
                            Threshold = 0.5
                        },
                        Strategies = new List<PhEyeFilterStrategy> { new() } // defaults to REDACT
                    }
                }
            }
        };
    }

    [Fact]
    public void Filter_LocalModelPolicy_DetectsPersonSpanFromText()
    {
        var result = new FilterService().Filter(LocalModelPersonPolicy(), "ctx", 0, "Alice Bob");

        var span = Assert.Single(result.Spans);
        Assert.Equal("Alice Bob", span.Text);
        Assert.Equal(0, span.CharacterStart);
        Assert.Equal(9, span.CharacterEnd);
        Assert.Equal(FilterType.Person, span.FilterType);
        Assert.Equal("person", span.Classification);
    }

    [Fact]
    public void Filter_LocalModelPolicy_RedactsDetectedPersonInOutputText()
    {
        var result = new FilterService().Filter(LocalModelPersonPolicy(), "ctx", 0, "Alice Bob");

        // The detected name is replaced in the output, end to end through the policy's REDACT strategy.
        Assert.DoesNotContain("Alice Bob", result.FilteredText);
        Assert.Contains("REDACTED", result.FilteredText);
    }

    [Fact]
    public void Filter_LocalModelPolicy_NoDetectionLeavesTextUnchanged()
    {
        // Empty input takes GlinerModel's early-out; the document flows through untouched.
        var result = new FilterService().Filter(LocalModelPersonPolicy(), "ctx", 0, "");

        Assert.Empty(result.Spans);
        Assert.Equal("", result.FilteredText);
    }
}
