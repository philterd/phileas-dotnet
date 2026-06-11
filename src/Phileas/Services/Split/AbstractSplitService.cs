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

/// <summary>Base class for split services providing shared cleanup of split output.</summary>
public abstract class AbstractSplitService
{
    /// <summary>Trims each line and removes empty entries.</summary>
    protected static List<string> Clean(IEnumerable<string> lines)
    {
        return lines.Select(JavaTrim).Where(l => l.Length > 0).ToList();
    }

    /// <summary>
    ///     Trims like Java's <c>String.trim()</c>: removes leading/trailing characters at or below
    ///     U+0020, which covers control characters (e.g. a trailing DOS EOF) that .NET's
    ///     whitespace-based <c>Trim()</c> leaves in place.
    /// </summary>
    private static string JavaTrim(string value)
    {
        var start = 0;
        var end = value.Length;
        while (start < end && value[start] <= ' ') start++;
        while (end > start && value[end - 1] <= ' ') end--;
        return value[start..end];
    }
}
