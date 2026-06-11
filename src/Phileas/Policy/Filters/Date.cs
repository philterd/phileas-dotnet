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
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy.Filters;

/// <summary>
///     Policy configuration for detecting date expressions.
/// </summary>
public class Date : AbstractPolicyFilter
{
    /// <summary>
    ///     Gets or sets a value indicating whether only dates that parse as real calendar dates are
    ///     redacted. When <see langword="false" /> (the default), well-formed but invalid dates such as
    ///     <c>02-31-2019</c> are still redacted.
    /// </summary>
    [JsonPropertyName("onlyValidDates")]
    public bool OnlyValidDates { get; set; } = false;

    /// <summary>Gets or sets the list of date filter strategies to apply.</summary>
    [JsonPropertyName("dateFilterStrategies")]
    public List<DateFilterStrategy>? Strategies { get; set; }
}