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
///     Disambiguates the filter types of spans that share the same start and end indexes but were
///     classified differently by competing filters.
/// </summary>
public interface ISpanDisambiguationService
{
    /// <summary>Hashes the span's context window and records it as training data for its filter type.</summary>
    /// <param name="context">The context.</param>
    /// <param name="span">The span.</param>
    void HashAndInsert(string context, Span span);

    /// <summary>
    ///     Resolves an ambiguous span to the candidate filter type whose accumulated context vector its
    ///     window most closely matches.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="filterTypes">The candidate filter types (the span's own type must be included).</param>
    /// <param name="ambiguousSpan">The ambiguous span.</param>
    /// <returns>The most likely filter type.</returns>
    FilterType Disambiguate(string context, IList<FilterType> filterTypes, Span ambiguousSpan);

    /// <summary>
    ///     Resolves a list of spans: competing spans (same location, different type) are resolved by
    ///     context, and unambiguous spans are recorded as training data. Resolved duplicates are deduped.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="spans">The spans.</param>
    /// <returns>The disambiguated spans.</returns>
    IList<Span> Disambiguate(string context, IList<Span> spans);
}
