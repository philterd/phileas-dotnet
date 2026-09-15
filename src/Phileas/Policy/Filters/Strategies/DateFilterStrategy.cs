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
    /// <summary>Gets or sets the number of days to shift the date when using the <c>SHIFT</c> strategy.</summary>
    [JsonPropertyName("shiftDays")]
    public int ShiftDays { get; set; } = 0;

    /// <summary>Gets or sets the number of months to shift the date when using the <c>SHIFT</c> strategy.</summary>
    [JsonPropertyName("shiftMonths")]
    public int ShiftMonths { get; set; } = 0;

    /// <summary>Gets or sets the number of years to shift the date when using the <c>SHIFT</c> strategy.</summary>
    [JsonPropertyName("shiftYears")]
    public int ShiftYears { get; set; } = 0;

    /// <summary>
    ///     Gets or sets a value indicating whether the date is shifted by a random amount rather than by
    ///     the configured offsets. Matches the Java filter's range: one to twenty-nine days forward, one
    ///     to eleven months forward, and one or two years back. Defaults to <see langword="false" />.
    /// </summary>
    [JsonPropertyName("shiftRandom")]
    public bool ShiftRandom { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether a shifted date may land in the future. When
    ///     <see langword="false" />, which is the default, a shift that would move a past date beyond
    ///     today is applied in the opposite direction instead, so the result stays in the past.
    /// </summary>
    [JsonPropertyName("futureDates")]
    public bool FutureDates { get; set; } = false;
}