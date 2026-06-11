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
///     A snapshot of the document after a single redaction was applied, with the SHA-256 hash of that
///     snapshot. The sequence of incremental redactions provides an auditable trail of how the final
///     filtered text was produced.
/// </summary>
public class IncrementalRedaction
{
    /// <summary>Creates a new <see cref="IncrementalRedaction" />.</summary>
    /// <param name="hash">The SHA-256 hex hash of <paramref name="incrementallyRedactedText" />.</param>
    /// <param name="span">The span whose replacement produced this snapshot.</param>
    /// <param name="incrementallyRedactedText">The document text after this redaction was applied.</param>
    public IncrementalRedaction(string hash, Span span, string incrementallyRedactedText)
    {
        Hash = hash;
        Span = span;
        IncrementallyRedactedText = incrementallyRedactedText;
    }

    /// <summary>Gets the SHA-256 hex hash of the snapshot.</summary>
    public string Hash { get; }

    /// <summary>Gets the span whose replacement produced this snapshot.</summary>
    public Span Span { get; }

    /// <summary>Gets the document text after this redaction was applied.</summary>
    public string IncrementallyRedactedText { get; }
}
