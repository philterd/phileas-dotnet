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

namespace Phileas.Rest;

/// <summary>The <c>/explain</c> response: the redacted text plus the applied and ignored spans.</summary>
public sealed record ExplainResponse(
    string FilteredText,
    string Context,
    string DocumentId,
    Explanation Explanation);

/// <summary>The span detail carried by an <see cref="ExplainResponse" />.</summary>
public sealed record Explanation(
    IReadOnlyList<SpanDto> AppliedSpans,
    IReadOnlyList<SpanDto> IgnoredSpans);

/// <summary>A single detected entity, projected to the fields relevant to an API caller.</summary>
public sealed record SpanDto(
    int CharacterStart,
    int CharacterEnd,
    string Text,
    string Replacement,
    string? Classification,
    double Confidence)
{
    public static SpanDto From(Span span) => new(
        span.CharacterStart,
        span.CharacterEnd,
        span.Text,
        span.Replacement,
        span.Classification,
        span.Confidence);
}

/// <summary>Request body for creating/updating a policy: the canonical Phileas policy JSON.</summary>
/// <param name="Json">The policy document as canonical Phileas policy JSON.</param>
public sealed record PolicyUpsertRequest(string Json);
