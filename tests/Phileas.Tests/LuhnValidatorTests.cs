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

public class LuhnValidatorTests
{
    [Fact]
    public void Registered() => Assert.IsType<LuhnValidator>(IdentifierValidators.FromPolicy(new Validator("luhn")));

    [Theory]
    [InlineData("046454286", true)]
    [InlineData("046 454 286", true)]
    [InlineData("046-454-286", true)]
    [InlineData(" 046-454 286 ", true)]
    [InlineData("4111111111111111", true)]
    [InlineData("91", true)]
    [InlineData("046454287", false)]
    [InlineData("123456789", false)]
    [InlineData("4111111111111112", false)]
    [InlineData("", false)]
    [InlineData("---", false)]
    public void IsValid(string text, bool expected) => Assert.Equal(expected, LuhnValidator.IsValid(text));

    [Fact]
    public void Null_is_invalid() => Assert.False(LuhnValidator.IsValid(null));
}
