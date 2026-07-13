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

namespace Phileas.Rest;

/// <summary>
///     Bound from the <c>Phileas</c> configuration section (appsettings.json / environment variables). Every
///     value has a container-friendly default so the service can start with only a Mongo and Valkey URI set.
/// </summary>
public sealed class PhileasRestOptions
{
    public const string SectionName = "Phileas";

    /// <summary>MongoDB connection string (source of truth for policies, contexts, and context entries).</summary>
    public string MongoConnectionString { get; set; } = "mongodb://localhost:27017";

    /// <summary>MongoDB database name.</summary>
    public string MongoDatabase { get; set; } = "phileas";

    /// <summary>
    ///     Valkey (Redis-protocol) connection string used as the context cache. When empty, the service runs
    ///     without a cache and reads/writes context entries straight through to MongoDB.
    /// </summary>
    public string ValkeyConnectionString { get; set; } = "localhost:6379";

    /// <summary>Time-to-live, in seconds, for cached context entries in Valkey. Zero disables expiry.</summary>
    public int ContextCacheTtlSeconds { get; set; } = 3600;

    /// <summary>
    ///     Filesystem path to the local GLiNER model directory baked into the image. When set, it is injected
    ///     into every policy's PhEye configuration that does not already specify a <c>modelPath</c>, so PII
    ///     detection runs in-process with no external PhEye service. Leave empty to require policies to configure
    ///     detection themselves (e.g. a remote PhEye endpoint).
    /// </summary>
    public string PhEyeModelPath { get; set; } = string.Empty;

    /// <summary>Optical character recognition settings for scanned/image PDFs.</summary>
    public OcrOptions Ocr { get; set; } = new();

    /// <summary>Excel (.xlsx) redaction settings.</summary>
    public XlsxOptions Xlsx { get; set; } = new();
}

/// <summary>Excel (.xlsx) redaction configuration.</summary>
public sealed class XlsxOptions
{
    /// <summary>
    ///     Default for whether each column's header is used as leading detection context for its data cells.
    ///     Overridable per request via the <c>/filter</c> <c>headerContext</c> query parameter.
    /// </summary>
    public bool UseHeaderContext { get; set; } = true;
}

/// <summary>How the PDF text extractor should use OCR.</summary>
public enum OcrMode
{
    /// <summary>No OCR — extract only the PDF text layer (default). Scanned/image-only pages yield no text.</summary>
    Off,

    /// <summary>Extract the text layer, and OCR only the pages that have no extractable text.</summary>
    Fallback,

    /// <summary>OCR every page, ignoring any existing text layer.</summary>
    Always
}

/// <summary>Tesseract OCR configuration. Only used when <see cref="Mode" /> is not <see cref="OcrMode.Off" />.</summary>
public sealed class OcrOptions
{
    /// <summary>When and whether OCR runs. Defaults to <see cref="OcrMode.Off" />.</summary>
    public OcrMode Mode { get; set; } = OcrMode.Off;

    /// <summary>Tesseract language(s), e.g. <c>eng</c> or <c>eng+fra</c>. Defaults to <c>eng</c>.</summary>
    public string Language { get; set; } = "eng";

    /// <summary>
    ///     Path to the Tesseract <c>tessdata</c> directory (holding the <c>&lt;lang&gt;.traineddata</c> files).
    ///     Defaults to the Debian package location used by the Docker image.
    /// </summary>
    public string TessDataPath { get; set; } = "/usr/share/tesseract-ocr/5/tessdata";

    /// <summary>DPI at which pages are rasterized before OCR. Higher is more accurate but slower. Defaults to 300.</summary>
    public int Dpi { get; set; } = 300;
}
