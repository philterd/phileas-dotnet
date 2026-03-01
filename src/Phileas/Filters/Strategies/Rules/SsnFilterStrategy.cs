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
using Phileas.Policy;

namespace Phileas.Filters.Strategies.Rules;

/// <summary>
///     Runtime filter strategy for Social Security Number (SSN) detection. Delegates to
///     <see cref="Phileas.Filters.Strategies.StandardFilterStrategy" /> with <c>FilterType.Ssn</c>.
/// </summary>
public class SsnFilterStrategy : StandardFilterStrategy
{
    /// <inheritdoc />
    public override Replacement GetReplacement(string context, string token, string[] window, double confidence,
        string? classification, FilterPattern? filterPattern, Crypto? crypto, Fpe? fpe)
    {
        return GetStandardReplacement(context, token, window, confidence, classification, filterPattern, crypto, fpe,
            FilterType.Ssn);
    }
}