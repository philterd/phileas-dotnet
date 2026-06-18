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

using System.Net.Http;
using Phileas.Filters;
using Phileas.Filters.PhEye;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;
using AbstractFilterStrategy = Phileas.Filters.AbstractFilterStrategy;

namespace Phileas.Tests;

/// <summary>
///     End-to-end tests against the published <c>philterd/ph-eye-pii-en-xsmall</c> model. They download the model
///     directly from Hugging Face (the int8 ONNX graph, the SentencePiece tokenizer, and the GLiNER config), run real
///     in-process inference, and assert the model identifies "George Washington" in text. These exercise the full
///     local-inference path against real weights, complementing the synthetic-fixture tests (which verify the pipeline
///     mechanics) and the <see cref="ModelFactAttribute" /> parity tests (which use an out-of-band model directory).
///
///     They are opt-in via <see cref="DownloadModelFactAttribute" /> (<c>PHILEAS_DOWNLOAD_MODEL=1</c>) so the default
///     suite stays offline; downloaded files are cached under the temp directory.
/// </summary>
public class XsmallEndToEndTests
{
    private const string Text = "George Washington was the first president of the United States.";

    [DownloadModelFact]
    public void Find_DetectsGeorgeWashington()
    {
        var dir = XsmallModel.EnsureDownloaded();
        using var model = new GlinerModel(dir);

        var entities = model.Find(Text, new List<string> { "name" }, 0.5);

        Assert.NotEmpty(entities);

        // The model may return one full-name span or separate first/last spans, so assert that every
        // non-space character of "George Washington" is covered by some detected span.
        var start = Text.IndexOf("George Washington", StringComparison.Ordinal);
        var end = start + "George Washington".Length;
        for (var i = start; i < end; i++)
        {
            if (Text[i] == ' ')
                continue;
            var index = i;
            Assert.True(
                entities.Any(e => e.Start <= index && index < e.End),
                "'George Washington' was not fully covered. Detected: " +
                string.Join(", ", entities.Select(e => $"'{e.Text}'[{e.Start},{e.End}]")));
        }
    }

    [DownloadModelFact]
    public void PhEyeFilter_DetectsGeorgeWashington()
    {
        var dir = XsmallModel.EnsureDownloaded();

        var config = new FilterConfiguration.Builder()
            .WithStrategies(new List<AbstractFilterStrategy>())
            .WithIgnored(new HashSet<string>(StringComparer.OrdinalIgnoreCase))
            .WithIgnoredPatterns(new List<IgnoredPattern>())
            .WithWindowSize(5)
            .WithPriority(0)
            .Build();

        var phEyeConfig = new PhEyeConfiguration
        {
            ModelPath = dir,
            Labels = new List<string> { "name" },
            Threshold = 0.5
        };

        // ModelPath is set, so inference runs locally and the HttpClient is unused.
        var filter = new PhEyeFilter(config, phEyeConfig, false, new Dictionary<string, double>(), new HttpClient());

        var policy = new PhileasPolicy
        {
            Name = "xsmall-e2e",
            Identifiers = new Identifiers
            {
                PhEyes = new List<PhEye> { new() { PhEyeConfiguration = phEyeConfig } }
            }
        };

        var result = filter.Filter(policy, "ctx", 0, Text);

        Assert.NotEmpty(result.Spans);
        Assert.Contains(result.Spans, s => s.Text.Contains("Washington") || s.Text.Contains("George"));
    }
}

/// <summary>
///     Downloads and caches the <c>philterd/ph-eye-pii-en-xsmall</c> model files into a temp directory laid out the
///     way <see cref="GlinerModel" /> expects (<c>gliner_config.json</c>, <c>spm.model</c>, and
///     <c>onnx/model.onnx</c>), and returns that directory.
/// </summary>
internal static class XsmallModel
{
    private const string BaseUrl = "https://huggingface.co/philterd/ph-eye-pii-en-xsmall/resolve/main/";

    // (path relative to the model directory, remote path under the repo)
    private static readonly (string Local, string Remote)[] Files =
    {
        ("gliner_config.json", "gliner_config.json"),
        ("spm.model", "spm.model"),
        (Path.Combine("onnx", "model.onnx"), "onnx/model.onnx")
    };

    public static string EnsureDownloaded()
    {
        var dir = Path.Combine(Path.GetTempPath(), "phileas-ph-eye-pii-en-xsmall");
        Directory.CreateDirectory(dir);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        foreach (var (local, remote) in Files)
        {
            var dest = Path.Combine(dir, local);
            if (File.Exists(dest) && new FileInfo(dest).Length > 0)
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            using var response = http
                .GetAsync(BaseUrl + remote, HttpCompletionOption.ResponseHeadersRead)
                .GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            using var source = response.Content.ReadAsStream();
            using var file = File.Create(dest);
            source.CopyTo(file);
        }

        return dir;
    }
}
