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

using System.Security.Cryptography;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Services;

/// <summary>
///     Selects filter requests for human spot review at the rate a policy's <c>config.review.samplingRate</c>
///     sets. Selection is per request and stateless. It has its own random source, so drawing never changes the
///     values drawn for anonymization and never changes the redacted output.
/// </summary>
public static class ReviewSampler
{
    /// <summary>
    ///     Draws once for a request filtered with <paramref name="policy" />. Call this once per request, not per
    ///     piece, page, or cell. <see cref="FilterService" /> and <see cref="Pdf.PdfFilterService" /> call it
    ///     themselves; a caller redacting a Word or Excel document calls it once for the document.
    /// </summary>
    /// <returns><see langword="true" /> if the request is selected for review.</returns>
    /// <exception cref="Phileas.Policy.PolicyValidationException">The sampling rate is not from 0.0 to 1.0.</exception>
    public static bool IsSelected(PhileasPolicy policy) => IsSelected(policy, NextDouble);

    internal static bool IsSelected(PhileasPolicy policy, Func<double> draw)
    {
        var review = policy.Config?.Review;
        if (review == null)
            return false;

        review.Validate();

        // Draws only when the outcome is not already decided. A draw from [0, 1) is always below 1.0.
        return review.SamplingRate switch
        {
            0.0 => false,
            1.0 => true,
            var rate => draw() < rate
        };
    }

    // A uniform value in [0, 1) from the top 53 bits of a cryptographic draw.
    private static double NextDouble()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return (BitConverter.ToUInt64(bytes) >> 11) * (1.0 / (1UL << 53));
    }
}
