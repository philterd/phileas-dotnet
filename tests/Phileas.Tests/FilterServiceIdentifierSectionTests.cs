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

using Phileas.Model;
using Phileas.Policy.Filters;
using Phileas.Services;
using PolicyIdentifiers = Phileas.Policy.Identifiers;
using PhileasPolicy = Phileas.Policy.Policy;
using Xunit;

namespace Phileas.Tests;

public class FilterServiceIdentifierSectionTests
{
    [Fact]
    public void FilterService_RunsCustomIdentifierFilter()
    {
        var policy = new PhileasPolicy
        {
            Identifiers = new PolicyIdentifiers { CustomIdentifiers = new List<Identifier> { new() } }
        };

        var result = new FilterService().Filter(policy, "ctx", 0, "the id is AB4736021 in california.");

        Assert.Contains(result.Spans, s => s is { Text: "AB4736021", FilterType: FilterType.Identifier });
    }

    [Fact]
    public void FilterService_RunsSectionFilter()
    {
        var policy = new PhileasPolicy
        {
            Identifiers = new PolicyIdentifiers
            {
                Sections = new List<Section>
                {
                    new() { StartPattern = "BEGIN-REDACT", EndPattern = "END-REDACT" }
                }
            }
        };

        var result = new FilterService().Filter(policy, "ctx", 0,
            "before BEGIN-REDACT secret stuff END-REDACT after");

        Assert.Contains(result.Spans, s => s.FilterType == FilterType.Section);
        Assert.DoesNotContain("secret stuff", result.FilteredText);
    }
}
