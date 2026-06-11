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

using Phileas.Filters;
using Phileas.Model;
using Phileas.Policy.Filters;
using PolicyIdentifiers = Phileas.Policy.Identifiers;
using PhileasPolicy = Phileas.Policy.Policy;
using RuntimeStrategy = Phileas.Filters.AbstractFilterStrategy;

namespace Phileas.Tests;

internal static class IdentifierSectionTestSupport
{
    public const int Piece = 0;

    /// <summary>A policy enabling the custom-identifier and section filters (so HasFilter returns true).</summary>
    public static PhileasPolicy GetPolicy()
    {
        return new PhileasPolicy
        {
            Identifiers = new PolicyIdentifiers
            {
                CustomIdentifiers = new List<Identifier> { new() },
                Sections = new List<Section> { new() }
            }
        };
    }

    public static FilterConfiguration Config(RuntimeStrategy strategy)
    {
        return new FilterConfiguration.Builder()
            .WithStrategies(new List<RuntimeStrategy> { strategy })
            .WithIgnored(new HashSet<string>())
            .WithIgnoredPatterns(new List<Phileas.Policy.IgnoredPattern>())
            .WithWindowSize(3)
            .Build();
    }

    public static bool CheckSpan(Span span, int start, int end, FilterType filterType)
    {
        return span.CharacterStart == start && span.CharacterEnd == end && span.FilterType == filterType;
    }
}
