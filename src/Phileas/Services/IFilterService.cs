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
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Services;

/// <summary>
///     Defines the contract for applying a <see cref="PhileasPolicy" /> to plain text.
/// </summary>
public interface IFilterService
{
    /// <summary>
    ///     Filters the supplied <paramref name="input" /> text according to the given <paramref name="policy" />
    ///     and returns the redacted text together with the detected <see cref="Span" /> objects.
    /// </summary>
    /// <param name="policy">The policy that controls which filters are active and how replacements are made.</param>
    /// <param name="context">An arbitrary context identifier used for referential-integrity replacements.</param>
    /// <param name="piece">A zero-based index identifying the segment of a multi-part document.</param>
    /// <param name="input">The plain-text input to filter.</param>
    /// <param name="contextService">
    ///     Optional service for persisting token-to-replacement mappings across calls.
    ///     Defaults to a fresh <see cref="InMemoryContextService" /> when <see langword="null" />.
    /// </param>
    /// <returns>A <see cref="TextFilterResult" /> containing the redacted text and identified spans.</returns>
    TextFilterResult Filter(PhileasPolicy policy, string context, int piece, string input,
        IContextService? contextService = null);

    /// <summary>
    ///     Filters the <paramref name="input" /> text and then computes precision, recall, and F1 score
    ///     by comparing the detected spans against the supplied ground-truth spans.
    /// </summary>
    /// <param name="policy">The policy that controls which filters are active and how replacements are made.</param>
    /// <param name="context">An arbitrary context identifier.</param>
    /// <param name="piece">A zero-based index identifying the segment of a multi-part document.</param>
    /// <param name="input">The plain-text input to filter.</param>
    /// <param name="groundTruthSpans">Known-correct spans used as the evaluation baseline.</param>
    /// <param name="contextService">
    ///     Optional service for persisting token-to-replacement mappings.
    ///     Defaults to a fresh <see cref="InMemoryContextService" /> when <see langword="null" />.
    /// </param>
    /// <returns>An <see cref="EvaluationResult" /> with precision, recall, and F1 values.</returns>
    EvaluationResult Evaluate(PhileasPolicy policy, string context, int piece, string input,
        IList<Span> groundTruthSpans, IContextService? contextService = null);
}