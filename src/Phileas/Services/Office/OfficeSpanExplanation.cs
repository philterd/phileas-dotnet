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

namespace Phileas.Services.Office;

/// <summary>
///     Copies the engine's "why was this flagged" detail from a <see cref="Span"/> onto an
///     <see cref="OfficeRedactionSpan"/>. Phileas has no separate <c>explain</c> call — every detection it
///     returns already carries this detail — so it is captured at redaction time.
/// </summary>
internal static class OfficeSpanExplanation
{
    public static void Populate(OfficeRedactionSpan entity, Span span)
    {
        entity.FilterType = span.FilterType.ToString();
        entity.Confidence = span.Confidence;
        entity.Pattern = span.Pattern ?? string.Empty;
        entity.Window = span.Window is { Length: > 0 } ? new List<string>(span.Window) : new List<string>();
    }
}
