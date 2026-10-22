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
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

// A ZIP+4 matched as an SSN or ITIN when an identifier's two separators could differ, so with the ZIP code filter
// enabled it was still redacted as ssn or itin. See #177 (philterd/phileas#460).
public class ZipPlus4RedactionTests
{
    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                Ssn = new Ssn(), Itin = new Itin(), CanadaSin = new CanadaSin(), ZipCode = new ZipCode()
            }
        };
    }

    [Theory]
    [InlineData("52168-1208")]
    [InlineData("02965-1837")]
    [InlineData("90210-1234")]
    public void ZipPlus4_IsRedactedAsAZipCode(string zip)
    {
        var result = new FilterService().Filter(CreatePolicy(), "ctx", 0, $"ships to {zip} today.");

        var span = Assert.Single(result.Spans);
        Assert.Equal(zip, span.Text);
        Assert.Equal(FilterType.ZipCode, span.FilterType);
        Assert.Equal("ships to {{{REDACTED-zip-code}}} today.", result.FilteredText);
    }

    [Theory]
    [InlineData("123-45-6789", FilterType.Ssn)]
    [InlineData("912-70-1234", FilterType.Itin)]
    [InlineData("046-454-286", FilterType.CanadaSin)]
    public void Identifiers_AreStillReportedAsThemselves(string value, FilterType type)
    {
        var result = new FilterService().Filter(CreatePolicy(), "ctx", 0, $"the value is {value}.");

        var span = Assert.Single(result.Spans);
        Assert.Equal(value, span.Text);
        Assert.Equal(type, span.FilterType);
    }
}
