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

using Phileas.Services.Disambiguation.Vector;

namespace Phileas.Services.Disambiguation;

/// <summary>
///     Builds the <see cref="ISpanDisambiguationService" /> appropriate for the options: the real
///     vector-based implementation when span disambiguation is enabled, or a
///     <see cref="NoOpSpanDisambiguationService" /> when it is disabled.
/// </summary>
public static class SpanDisambiguationServiceFactory
{
    /// <summary>
    ///     Returns the vector-based service if <see cref="SpanDisambiguationOptions.Enabled" />, otherwise a
    ///     no-op service.
    /// </summary>
    /// <param name="options">The options that decide whether disambiguation is enabled.</param>
    /// <param name="vectorService">The vector store backing the vector-based implementation.</param>
    public static ISpanDisambiguationService Create(SpanDisambiguationOptions options, IVectorService vectorService)
    {
        if (options.Enabled)
            return new VectorBasedSpanDisambiguationService(options, vectorService);

        return new NoOpSpanDisambiguationService();
    }
}
