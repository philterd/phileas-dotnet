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
///     The result of redacting a binary document (e.g. a PDF): the redacted bytes plus the spans that
///     were detected. Mirrors the Java <c>BinaryDocumentFilterResult</c>.
/// </summary>
public class BinaryDocumentFilterResult
{
    /// <summary>Creates a binary document filter result.</summary>
    /// <param name="document">The redacted document bytes.</param>
    /// <param name="context">The context identifier.</param>
    /// <param name="spans">The detected spans (with page numbers and coordinates).</param>
    /// <param name="tokens">The number of whitespace-delimited tokens in the source document.</param>
    public BinaryDocumentFilterResult(byte[] document, string context, IList<Span> spans, long tokens)
    {
        Document = document;
        Context = context;
        Spans = spans;
        Tokens = tokens;
    }

    /// <summary>Gets the redacted document bytes (a PDF or a ZIP of images depending on the requested format).</summary>
    public byte[] Document { get; }

    /// <summary>Gets the context identifier.</summary>
    public string Context { get; }

    /// <summary>Gets the spans that were detected and redacted.</summary>
    public IList<Span> Spans { get; }

    /// <summary>Gets the number of whitespace-delimited tokens in the source document.</summary>
    public long Tokens { get; }
}
