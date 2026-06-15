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

public class Mod11ValidatorTests
{
    private static Validator V(string json) => JsonSerializer.Deserialize<Identifier>(json)!.Validator!;

    [Fact]
    public void Registered_cpf() => Assert.IsType<Mod11Validator>(
        IdentifierValidators.FromPolicy(V("{\"validator\":{\"name\":\"mod11\",\"params\":{\"variant\":\"cpf\"}}}")));

    [Theory]
    [InlineData("52998224725", true)]
    [InlineData("529.982.247-25", true)]
    [InlineData("52998224724", false)]
    [InlineData("11111111111", false)]
    [InlineData("5299822472", false)]
    public void Cpf(string text, bool expected) => Assert.Equal(expected, Mod11Validator.IsValidCpf(text));

    [Theory]
    [InlineData("11222333000181", true)]
    [InlineData("11.222.333/0001-81", true)]
    [InlineData("11222333000182", false)]
    [InlineData("00000000000000", false)]
    public void Cnpj(string text, bool expected) => Assert.Equal(expected, Mod11Validator.IsValidCnpj(text));

    [Fact]
    public void Missing_variant_throws() =>
        Assert.Throws<ArgumentException>(() => IdentifierValidators.FromPolicy(new Validator("mod11")));

    [Fact]
    public void Unknown_variant_throws() =>
        Assert.Throws<ArgumentException>(() =>
            IdentifierValidators.FromPolicy(V("{\"validator\":{\"name\":\"mod11\",\"params\":{\"variant\":\"rut\"}}}")));
}
