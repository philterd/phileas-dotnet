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

using System.Text.Json.Serialization;

namespace Phileas.Policy.Filters.Strategies;

/// <summary>
///     Defines the replacement strategy settings for a date expression filter, as deserialized from a policy JSON
///     document.
/// </summary>
public class DateFilterStrategy : AbstractFilterStrategy
{
    /// <summary>Gets or sets the number of days to shift the date when using the <c>SHIFT_DATE</c> strategy.</summary>
    [JsonPropertyName("days")]
    public int Days { get; set; } = 0;

    /// <summary>Gets or sets the number of months to shift the date when using the <c>SHIFT_DATE</c> strategy.</summary>
    [JsonPropertyName("months")]
    public int Months { get; set; } = 0;

    /// <summary>Gets or sets the number of years to shift the date when using the <c>SHIFT_DATE</c> strategy.</summary>
    [JsonPropertyName("years")]
    public int Years { get; set; } = 0;
}