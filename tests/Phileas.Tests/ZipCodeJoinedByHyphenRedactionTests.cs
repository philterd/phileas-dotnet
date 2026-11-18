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

using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

// Five digits joined by a hyphen to a letter or digit before them were redacted as a ZIP code, leaving the rest
// of a case number or reference code in the output. See #193 (philterd/phileas#485).
public class ZipCodeJoinedByHyphenRedactionTests
{
    private static PhileasPolicy CreatePolicy()
    {
        return new PhileasPolicy
        {
            Name = "test",
            Identifiers = new Identifiers
            {
                Ssn = new Ssn(),
                PhoneNumber = new PhoneNumber(),
                Date = new Date(),
                ZipCode = new ZipCode(),
                Age = new Age(),
                Currency = new Currency()
            }
        };
    }

    [Theory]
    [InlineData("Case 2026-00847 filed.")]
    [InlineData("Ref FR-48213 here.")]
    public void DigitsJoinedByAHyphen_AreNotRedacted(string input)
    {
        var result = new FilterService().Filter(CreatePolicy(), "ctx", 0, input);

        Assert.Equal(input, result.FilteredText);
    }

    [Fact]
    public void ZipCodeAfterASpace_IsStillRedacted()
    {
        var result = new FilterService().Filter(CreatePolicy(), "ctx", 0, "Send it to Boston, MA 02116.");

        Assert.Equal("Send it to Boston, MA {{{REDACTED-zip-code}}}.", result.FilteredText);
    }
}
