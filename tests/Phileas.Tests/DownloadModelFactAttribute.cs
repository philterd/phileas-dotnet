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
///     A <see cref="FactAttribute" /> for end-to-end tests that download a real model from Hugging Face. These pull
///     roughly 90 MB over the network and run real ONNX inference, so they are opt-in: unless
///     <c>PHILEAS_DOWNLOAD_MODEL=1</c> is set, the test is reported as <em>skipped</em> (with a reason) rather than
///     run, so the default suite stays fast and offline. Set the variable to exercise the full download-and-detect
///     path. The downloaded files are cached under the temp directory, so repeated opted-in runs do not re-download.
/// </summary>
public sealed class DownloadModelFactAttribute : FactAttribute
{
    public DownloadModelFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PHILEAS_DOWNLOAD_MODEL") != "1")
            Skip = "Set PHILEAS_DOWNLOAD_MODEL=1 to run the end-to-end test that downloads ph-eye-pii-en-xsmall " +
                   "from Hugging Face (~90 MB) and runs local inference.";
    }
}
