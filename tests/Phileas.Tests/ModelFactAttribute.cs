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

using Xunit;

namespace Phileas.Tests;

/// <summary>
///     A <see cref="FactAttribute" /> for tests that need a real GLiNER model directory. When the
///     <c>PHILEAS_GLINER_MODEL_DIR</c> environment variable is not set to an existing directory, the test is reported
///     as <em>skipped</em> (with a reason) rather than passing. This keeps the end-to-end ONNX parity tests honest:
///     absent a model they show up as skipped in the test report, not as a silent green pass that implies coverage the
///     run never exercised. The model is far too large to vendor, so it is supplied out-of-band when verifying parity.
/// </summary>
public sealed class ModelFactAttribute : FactAttribute
{
    public ModelFactAttribute()
    {
        var dir = Environment.GetEnvironmentVariable("PHILEAS_GLINER_MODEL_DIR");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            Skip = "Set PHILEAS_GLINER_MODEL_DIR to a GLiNER model directory to run the local-inference parity tests.";
    }
}
