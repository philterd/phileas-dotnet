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

namespace Phileas.Services.Generators;

/// <summary>
///     Re-scans a candidate replacement produced by an <see cref="IReplacementGenerator" /> to confirm the generator
///     did not reintroduce sensitive information. A <c>MAP_REPLACE</c> strategy rejects a generated value that contains
///     PII and applies its fallback strategy instead, so a generator can never emit new sensitive information into the
///     output.
/// </summary>
public interface IReplacementValidator
{
    /// <summary>
    ///     Determines whether a candidate replacement contains detectable PII.
    /// </summary>
    /// <param name="candidate">The generated replacement value to re-scan.</param>
    /// <returns><see langword="true" /> if the candidate contains detectable PII (and must be rejected); otherwise <see langword="false" />.</returns>
    bool ContainsPii(string candidate);
}
