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
///     Configures the selection of filter requests for human spot review, as specified in philterd/phisql#84.
/// </summary>
public class Review
{
    /// <summary>
    ///     Gets or sets the probability, from 0.0 to 1.0, that a filter request is selected for human spot
    ///     review. Defaults to 0.0, which selects none.
    /// </summary>
    [JsonPropertyName("samplingRate")]
    public double SamplingRate { get; set; } = 0.0;

    /// <summary>Throws <see cref="PolicyValidationException" /> when <see cref="SamplingRate" /> is not from 0.0 to 1.0.</summary>
    public void Validate()
    {
        // Written so NaN fails too.
        if (!(SamplingRate >= 0.0 && SamplingRate <= 1.0))
            throw new PolicyValidationException(
                new[] { $"/config/review/samplingRate: {SamplingRate} is not a number from 0.0 to 1.0" },
                "The policy's review sampling rate is not valid");
    }
}
