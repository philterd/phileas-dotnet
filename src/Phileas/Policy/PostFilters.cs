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

namespace Phileas.Policy;

/// <summary>
///     Configures the post-filtering operations that are applied to detected spans after initial matching.
///     Post-filters trim extraneous characters (trailing spaces, periods, and newlines) from the ends of
///     matched entities so that punctuation is not inadvertently included in a redacted span.
/// </summary>
public class PostFilters
{
    /// <summary>
    ///     Gets or sets a value indicating whether trailing newline characters (<c>\n</c>, <c>\r</c>) are stripped
    ///     from the end of matched entity text. Defaults to <see langword="true" />.
    /// </summary>
    [JsonPropertyName("trailingNewLines")] public bool TrailingNewLines { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether trailing period characters (<c>.</c>) are stripped from the end
    ///     of matched entity text. Defaults to <see langword="true" />.
    /// </summary>
    [JsonPropertyName("trailingPeriods")] public bool TrailingPeriods { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether trailing space characters are stripped from the end of matched
    ///     entity text. Defaults to <see langword="true" />.
    /// </summary>
    [JsonPropertyName("trailingSpaces")] public bool TrailingSpaces { get; set; } = true;
}