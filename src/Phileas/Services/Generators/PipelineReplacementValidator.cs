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

using Phileas.Filters;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Services.Generators;

/// <summary>
///     <see cref="IReplacementValidator" /> that re-scans a candidate replacement by running the policy's filter
///     pipeline over it: if any filter detects a span, the candidate contains PII and is rejected. A throwaway context
///     name isolates the re-scan from the caller's context, and a thread-local re-scan flag prevents a generator from
///     being invoked during the re-scan (which would recurse).
/// </summary>
public class PipelineReplacementValidator : IReplacementValidator
{
    private readonly IList<AbstractFilter> _filters;
    private readonly PhileasPolicy _policy;

    /// <summary>
    ///     Initializes a new <see cref="PipelineReplacementValidator" />.
    /// </summary>
    /// <param name="policy">The active policy, passed through to each filter during the re-scan.</param>
    /// <param name="filters">The full set of filters to run over a candidate replacement.</param>
    public PipelineReplacementValidator(PhileasPolicy policy, IList<AbstractFilter> filters)
    {
        _policy = policy;
        _filters = filters;
    }

    /// <inheritdoc />
    public bool ContainsPii(string candidate)
    {
        // Already inside a re-scan: report no PII to break any potential recursion.
        if (AbstractFilterStrategy.IsRescanning)
            return false;

        AbstractFilterStrategy.SetRescanning(true);
        try
        {
            // A unique context name isolates any replacement bookkeeping done during the re-scan from the
            // caller's real context. The context name is immaterial to detection over this fresh scope.
            var rescanContext = Guid.NewGuid().ToString();

            foreach (var filter in _filters)
                if (filter.Filter(_policy, rescanContext, 0, candidate).Spans.Count > 0)
                    return true;

            return false;
        }
        catch (Exception)
        {
            // A re-scan failure is treated as unsafe so a generated value is never emitted unchecked.
            return true;
        }
        finally
        {
            AbstractFilterStrategy.SetRescanning(false);
        }
    }
}
