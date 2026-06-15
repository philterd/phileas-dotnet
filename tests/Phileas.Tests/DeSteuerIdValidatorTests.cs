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

public class DeSteuerIdValidatorTests
{
    [Fact]
    public void Registered() =>
        Assert.IsType<DeSteuerIdValidator>(IdentifierValidators.FromPolicy(new Validator("de-steuerid")));

    [Theory]
    [InlineData("86095742719", true)]   // one digit repeated twice
    [InlineData("65929970489", true)]   // one digit repeated three times
    [InlineData("47036892816", true)]   // different check digit
    [InlineData("86 095 742 719", true)]
    [InlineData("86095742718", false)]  // wrong check digit
    [InlineData("12345678905", false)]  // no repeated digit
    [InlineData("11223456780", false)]  // two different digits repeated
    [InlineData("11110234567", false)]  // a digit repeated four times
    [InlineData("01234567890", false)]  // leading zero
    [InlineData("8609574271", false)]   // length 10
    [InlineData("860957427190", false)] // length 12
    [InlineData("8609574271A", false)]  // letter
    [InlineData("", false)]
    public void IsValid(string text, bool expected) => Assert.Equal(expected, DeSteuerIdValidator.IsValid(text));

    [Fact]
    public void Null_is_invalid() => Assert.False(DeSteuerIdValidator.IsValid(null));
}
