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

public class Mod23LetterValidatorTests
{
    private static readonly IReadOnlyDictionary<string, string> Subs = Mod23LetterValidator.DefaultPrefixSubstitutions;

    [Fact]
    public void Registered() =>
        Assert.IsType<Mod23LetterValidator>(IdentifierValidators.FromPolicy(new Validator("mod23-letter")));

    [Theory]
    [InlineData("12345678Z", true)]
    [InlineData("12345678z", true)]
    [InlineData("X1234567L", true)]
    [InlineData("Y1234567X", true)]
    [InlineData("12345678A", false)]
    [InlineData("X1234567A", false)]
    [InlineData("1234567Z", false)]
    [InlineData("123456781", false)]
    [InlineData("1234A678Z", false)]
    public void IsValid(string text, bool expected) => Assert.Equal(expected, Mod23LetterValidator.IsValid(text, Subs));

    [Fact]
    public void Null_is_invalid() => Assert.False(Mod23LetterValidator.IsValid(null, Subs));
}
