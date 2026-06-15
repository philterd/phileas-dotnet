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

public class EsCifValidatorTests
{
    [Fact]
    public void Registered() => Assert.IsType<EsCifValidator>(IdentifierValidators.FromPolicy(new Validator("es-cif")));

    [Theory]
    [InlineData("A58818501", true)]
    [InlineData("P1234567D", true)]
    [InlineData("p1234567d", true)]
    [InlineData("A58818502", false)]
    [InlineData("P1234567E", false)]
    [InlineData("I58818501", false)]
    [InlineData("A5881X501", false)]
    [InlineData("A5881850", false)]
    [InlineData("", false)]
    public void IsValid(string text, bool expected) => Assert.Equal(expected, EsCifValidator.IsValid(text));

    [Fact]
    public void Null_is_invalid() => Assert.False(EsCifValidator.IsValid(null));
}
