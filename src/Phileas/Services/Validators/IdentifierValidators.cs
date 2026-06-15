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

namespace Phileas.Services.Validators;

/// <summary>
///     Resolves the <c>validator</c> named on an <c>identifier</c> filter to its built-in
///     <see cref="ISpanValidator" />. An unknown or not-yet-implemented name is a loud policy error,
///     never silently ignored, so a policy can never quietly skip the check it asked for. Mirrors the
///     Java <c>IdentifierValidators</c>.
/// </summary>
public static class IdentifierValidators
{
    /// <summary>
    ///     Returns the <see cref="ISpanValidator" /> for the given policy validator, or
    ///     <see langword="null" /> when <paramref name="validator" /> is <see langword="null" />
    ///     (meaning: keep every match).
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     Thrown when the validator name is empty, unknown, or recognized by the schema but not
    ///     implemented in this build.
    /// </exception>
    public static ISpanValidator? FromPolicy(Validator? validator)
    {
        if (validator == null)
            return null;

        var name = validator.Name;

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("An identifier validator must have a non-empty name.");

        return name switch
        {
            "luhn" => LuhnValidator.GetInstance(),
            "mod11" => Mod11Validator.FromParams(validator.Params),
            "mod97" => Mod97Validator.FromParams(validator.Params),
            "mod23-letter" => Mod23LetterValidator.FromParams(validator.Params),
            "es-cif" => EsCifValidator.GetInstance(),
            "de-steuerid" => DeSteuerIdValidator.GetInstance(),
            "de-personalausweis" => DePersonalausweisValidator.GetInstance(),
            "bic-structural" => BicStructuralValidator.GetInstance(),
            _ => throw new ArgumentException(
                $"Unsupported identifier validator '{name}'. This build implements: luhn, mod11, mod97, "
                + "mod23-letter, es-cif, de-steuerid, de-personalausweis, bic-structural.")
        };
    }
}
