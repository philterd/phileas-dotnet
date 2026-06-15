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

public class DePersonalausweisValidatorTests
{
    [Fact]
    public void Registered() => Assert.IsType<DePersonalausweisValidator>(
        IdentifierValidators.FromPolicy(new Validator("de-personalausweis")));

    [Theory]
    [InlineData("T220001293", true)]
    [InlineData("M123456788", true)]
    [InlineData("t220001293", true)]
    [InlineData("  T220001293  ", true)]
    [InlineData("T220001294", false)]  // wrong check digit
    [InlineData("T220001393", false)]  // altered serial
    [InlineData("T22000129X", false)]  // check char not a digit
    [InlineData("T2200012*3", false)]  // invalid serial character
    [InlineData("T22000129", false)]   // length 9
    [InlineData("T2200012930", false)] // length 11
    [InlineData("", false)]
    public void IsValid(string text, bool expected) => Assert.Equal(expected, DePersonalausweisValidator.IsValid(text));

    [Fact]
    public void Null_is_invalid() => Assert.False(DePersonalausweisValidator.IsValid(null));
}
