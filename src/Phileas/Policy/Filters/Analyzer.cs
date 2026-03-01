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

using Phileas.Model.Filtering;

namespace Phileas.Filters;

/// <summary>
/// Groups one or more <see cref="FilterPattern"/> objects together with an optional set of
/// contextual terms. An <see cref="Analyzer"/> is used by <see cref="Phileas.Rules.Regex.RegexFilter"/>
/// to scan input text and produce candidate <see cref="Phileas.Model.Filtering.Span"/> objects.
/// </summary>
public class Analyzer
{
    /// <summary>Gets the set of contextual terms that increase match confidence when found near a detected entity.</summary>
    public ISet<string>? ContextualTerms { get; }

    /// <summary>Gets the list of filter patterns used to detect entities in input text.</summary>
    public IList<FilterPattern> FilterPatterns { get; }

    /// <summary>
    /// Initializes an <see cref="Analyzer"/> with the given patterns and no contextual terms.
    /// </summary>
    /// <param name="patterns">One or more <see cref="FilterPattern"/> objects.</param>
    public Analyzer(params FilterPattern[] patterns)
    {
        FilterPatterns = patterns.ToList();
    }

    /// <summary>
    /// Initializes an <see cref="Analyzer"/> with contextual terms and the given patterns.
    /// </summary>
    /// <param name="contextualTerms">Terms whose proximity to a match boosts confidence.</param>
    /// <param name="patterns">One or more <see cref="FilterPattern"/> objects.</param>
    public Analyzer(ISet<string> contextualTerms, params FilterPattern[] patterns)
    {
        ContextualTerms = contextualTerms;
        FilterPatterns = patterns.ToList();
    }
}
