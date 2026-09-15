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

namespace Phileas.Services.Split;

/// <summary>Creates the <see cref="ISplitService" /> for a policy's splitting configuration.</summary>
public static class SplitFactory
{
    /// <summary>The split method names this build accepts, in the order they are documented.</summary>
    private static readonly string[] Accepted = { "newline", "width", "characters", "character" };

    /// <summary>
    ///     Returns the split service for <paramref name="method" />: <c>"newline"</c>, <c>"width"</c>, or
    ///     <c>"characters"</c>. <c>"character"</c> is accepted as an alias, because the redaction policy
    ///     specification's own splitting example uses the singular.
    /// </summary>
    /// <param name="method">The split method named by the policy.</param>
    /// <param name="threshold">The character threshold the width and character-count methods use.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="method" /> is not one of the accepted names. An unknown method is
    ///     a policy error rather than a reason to substitute another: splitting silently by a method the
    ///     policy did not ask for produces pieces the author never intended, and any overlap they
    ///     configured applies to boundaries that are not the ones they expected.
    /// </exception>
    public static ISplitService GetSplitService(string method, int threshold)
    {
        if (string.Equals(method, "newline", StringComparison.OrdinalIgnoreCase))
            return new NewLineSplitService();

        if (string.Equals(method, "width", StringComparison.OrdinalIgnoreCase))
            return new LineWidthSplitService(threshold);

        if (string.Equals(method, "characters", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "character", StringComparison.OrdinalIgnoreCase))
            return new CharacterCountSplitService(threshold);

        throw new ArgumentException(
            $"Unsupported split method '{method}'. This build accepts: {string.Join(", ", Accepted)}.",
            nameof(method));
    }
}
