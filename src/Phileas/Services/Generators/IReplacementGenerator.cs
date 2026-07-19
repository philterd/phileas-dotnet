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
///     Produces a replacement value for a detected token. Used by the <c>MAP_REPLACE</c> filter strategy to generate a
///     replacement for a value that is absent from its lookup table.
/// </summary>
public interface IReplacementGenerator
{
    /// <summary>
    ///     Generates a replacement for a detected token.
    /// </summary>
    /// <param name="token">The detected value.</param>
    /// <param name="label">The entity label (filter type) of the detected value, or <see langword="null" />.</param>
    /// <returns>The generated replacement value.</returns>
    /// <exception cref="System.Exception">
    ///     Thrown if the generator fails, times out, or returns invalid output. The caller applies the strategy's
    ///     fallback in that case so the token is never left in the clear.
    /// </exception>
    string Generate(string token, string? label);
}
