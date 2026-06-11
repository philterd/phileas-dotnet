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

namespace Phileas.Model;

/// <summary>
///     The output formats supported by the binary (PDF) redaction pipeline.
/// </summary>
public enum MimeType
{
    /// <summary>A redacted PDF whose pages are rasterized images (no recoverable text layer).</summary>
    ApplicationPdf,

    /// <summary>A ZIP archive containing one rasterized, redacted image per page.</summary>
    ImageJpeg
}
