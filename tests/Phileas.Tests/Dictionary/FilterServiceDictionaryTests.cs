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

using Phileas.Policy.Filters;
using Phileas.Services;
using Xunit;
using PolicyIdentifiers = Phileas.Policy.Identifiers;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests.Dictionaries;

public class FilterServiceDictionaryTests
{
    [Fact]
    public void FilterService_RunsCityDictionaryFilter()
    {
        // A policy that enables the city filter is wired into a SetDictionaryFilter (fuzzy defaults off)
        // by FilterService, which detects a bundled city and redacts it.
        var policy = new PhileasPolicy
        {
            Identifiers = new PolicyIdentifiers { City = new City() }
        };

        var result = new FilterService().Filter(policy, "ctx", 0, "He visited Boston today.");

        Assert.Contains(result.Spans, s => s.Text == "Boston");
        Assert.DoesNotContain("Boston", result.FilteredText);
    }

    [Fact]
    public void FilterService_RunsCustomDictionaryFilter()
    {
        var policy = new PhileasPolicy
        {
            Identifiers = new PolicyIdentifiers
            {
                CustomDictionaries = new List<CustomDictionary>
                {
                    new() { Terms = new List<string> { "Acme", "Globex" } }
                }
            }
        };

        var result = new FilterService().Filter(policy, "ctx", 0, "I work at Globex.");

        Assert.Contains(result.Spans, s => s.Text == "Globex");
    }
}
