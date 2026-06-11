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

namespace Phileas.Model;

/// <summary>
///     Fuzzy-matching sensitivity for dictionary filters. Higher sensitivity requires a closer match.
/// </summary>
public enum SensitivityLevel
{
    /// <summary>Automatic sensitivity.</summary>
    Auto,

    /// <summary>Fuzzy matching disabled (exact matches only).</summary>
    Off,

    /// <summary>Loosest fuzzy matching (Levenshtein distance up to 2).</summary>
    Low,

    /// <summary>Moderate fuzzy matching (Levenshtein distance up to 1).</summary>
    Medium,

    /// <summary>Strictest fuzzy matching (exact tokens only).</summary>
    High
}

/// <summary>Parsing helpers for <see cref="SensitivityLevel" />.</summary>
public static class SensitivityLevels
{
    /// <summary>Parses a sensitivity name, defaulting to <see cref="SensitivityLevel.High" /> when unknown.</summary>
    public static SensitivityLevel FromName(string? name)
    {
        return name?.ToLowerInvariant() switch
        {
            "auto" => SensitivityLevel.Auto,
            "off" => SensitivityLevel.Off,
            "low" => SensitivityLevel.Low,
            "medium" => SensitivityLevel.Medium,
            "high" => SensitivityLevel.High,
            _ => SensitivityLevel.High
        };
    }
}
