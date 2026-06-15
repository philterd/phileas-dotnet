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
using Phileas.Services.Validators;
using Xunit;

namespace Phileas.Tests;

public class BicStructuralValidatorTests
{
    [Fact]
    public void Registered() => Assert.IsType<BicStructuralValidator>(
        IdentifierValidators.FromPolicy(new Validator("bic-structural")));

    [Theory]
    [InlineData("DEUTDEFF", true)]
    [InlineData("DEUTDEFF500", true)]
    [InlineData("BOFAUS3N", true)]
    [InlineData("NEDSZAJJ", true)]
    [InlineData("deutdeff", true)]
    [InlineData("  DEUTDEFF  ", true)]
    [InlineData("DEUTZZFF", false)]   // unassigned country
    [InlineData("DEUT12FF", false)]   // country not letters
    [InlineData("DEU1DEFF", false)]   // institution not letters
    [InlineData("DEUTDEF", false)]    // length 7
    [InlineData("DEUTDEFF5", false)]  // length 9
    [InlineData("DEUTDEFF50", false)] // length 10
    [InlineData("", false)]
    public void IsValid(string text, bool expected) => Assert.Equal(expected, BicStructuralValidator.IsValid(text));

    [Fact]
    public void Null_is_invalid() => Assert.False(BicStructuralValidator.IsValid(null));
}
