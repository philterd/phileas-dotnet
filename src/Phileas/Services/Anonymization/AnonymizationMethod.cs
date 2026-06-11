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

namespace Phileas.Services.Anonymization;

/// <summary>How an anonymization service produces a replacement value.</summary>
public enum AnonymizationMethod
{
    /// <summary>Generate a realistic fake value of the same kind as the token.</summary>
    Realistic,

    /// <summary>Pick a value from a supplied candidate list.</summary>
    FromList,

    /// <summary>Replace with a random UUID.</summary>
    Uuid
}

/// <summary>Parsing helpers for <see cref="AnonymizationMethod" />.</summary>
public static class AnonymizationMethods
{
    /// <summary>Parses a method name, defaulting to <see cref="AnonymizationMethod.Uuid" /> when null or unknown.</summary>
    public static AnonymizationMethod FromString(string? value)
    {
        if (value == null) return AnonymizationMethod.Uuid;
        if (value.Equals("realistic", StringComparison.OrdinalIgnoreCase)) return AnonymizationMethod.Realistic;
        if (value.Equals("from_list", StringComparison.OrdinalIgnoreCase)) return AnonymizationMethod.FromList;
        if (value.Equals("uuid", StringComparison.OrdinalIgnoreCase)) return AnonymizationMethod.Uuid;
        return AnonymizationMethod.Uuid;
    }

    /// <summary>Returns the canonical lowercase string for a method.</summary>
    public static string ToValue(this AnonymizationMethod method) => method switch
    {
        AnonymizationMethod.Realistic => "realistic",
        AnonymizationMethod.FromList => "from_list",
        _ => "uuid"
    };
}
