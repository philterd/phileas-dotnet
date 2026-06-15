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

using System.Text.Json;
using Phileas.Policy.Filters;
using Phileas.Services.Validators;
using Xunit;

namespace Phileas.Tests;

public class Mod97ValidatorTests
{
    private static readonly IReadOnlyDictionary<string, string> NirSubs = Mod97Validator.DefaultNirSubstitutions;

    private static Validator V(string json) => JsonSerializer.Deserialize<Identifier>(json)!.Validator!;

    [Fact]
    public void Registered_iban() => Assert.IsType<Mod97Validator>(
        IdentifierValidators.FromPolicy(V("{\"validator\":{\"name\":\"mod97\",\"params\":{\"variant\":\"iban\"}}}")));

    [Theory]
    [InlineData("255081416802538", true)]
    [InlineData("220032A00801642", true)]
    [InlineData("255081416802539", false)]
    [InlineData("25508141680253", false)]
    [InlineData("2200Q2A00801642", false)]
    public void Nir(string text, bool expected) => Assert.Equal(expected, Mod97Validator.IsValidNir(text, NirSubs));

    [Theory]
    [InlineData("GB82WEST12345698765432", true)]
    [InlineData("DE89 3704 0044 0532 0130 00", true)]
    [InlineData("GB82WEST12345698765431", false)]
    [InlineData("1234", false)]
    public void Iban(string text, bool expected) => Assert.Equal(expected, Mod97Validator.IsValidIban(text));

    [Fact]
    public void Null_is_invalid()
    {
        Assert.False(Mod97Validator.IsValidNir(null, NirSubs));
        Assert.False(Mod97Validator.IsValidIban(null));
    }

    [Fact]
    public void Missing_variant_throws() =>
        Assert.Throws<ArgumentException>(() => IdentifierValidators.FromPolicy(new Validator("mod97")));
}
