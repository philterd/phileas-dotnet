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
    /// <summary>
    ///     Returns the split service for <paramref name="method" /> (<c>"newline"</c>, <c>"width"</c>, or
    ///     <c>"characters"</c>); unknown methods fall back to newline splitting.
    /// </summary>
    public static ISplitService GetSplitService(string method, int threshold)
    {
        if (string.Equals(method, "newline", StringComparison.OrdinalIgnoreCase)) return new NewLineSplitService();
        if (string.Equals(method, "width", StringComparison.OrdinalIgnoreCase)) return new LineWidthSplitService(threshold);
        if (string.Equals(method, "characters", StringComparison.OrdinalIgnoreCase)) return new CharacterCountSplitService(threshold);
        return new NewLineSplitService();
    }
}
