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

namespace Phileas.Services.Disambiguation;

/// <summary>
///     A <see cref="ISpanDisambiguationService" /> that does nothing: used when span disambiguation is
///     disabled. It lets callers invoke disambiguation unconditionally (the
///     <see cref="SpanDisambiguationServiceFactory" /> decides which implementation is used), so the
///     pipeline needs no "is it enabled?" branch around the call.
/// </summary>
public class NoOpSpanDisambiguationService : ISpanDisambiguationService
{
    /// <inheritdoc />
    public void HashAndInsert(string context, Span span)
    {
        // Nothing to learn when disambiguation is disabled.
    }

    /// <inheritdoc />
    public FilterType Disambiguate(string context, IList<FilterType> filterTypes, Span ambiguousSpan)
    {
        // No information to disambiguate with; keep the first candidate.
        return filterTypes[0];
    }

    /// <inheritdoc />
    public IList<Span> Disambiguate(string context, IList<Span> spans)
    {
        // Pass the spans through untouched.
        return spans;
    }
}
