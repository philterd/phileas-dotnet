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

namespace Phileas.Services.Pdf;

/// <summary>Extracts positioned lines of text from a binary document.</summary>
public interface ITextExtractor
{
    /// <summary>Extracts the lines of text (with per-character coordinates) from the document.</summary>
    /// <param name="document">The document bytes.</param>
    /// <returns>The extracted lines, in reading order.</returns>
    IReadOnlyList<PdfLine> GetLines(byte[] document);
}
