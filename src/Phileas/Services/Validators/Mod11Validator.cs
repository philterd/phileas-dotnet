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
using Phileas.Model;

namespace Phileas.Services.Validators;

/// <summary>
///     Weighted-sum mod-11 check-digit validator. The <c>variant</c> parameter selects the scheme:
///     <c>cpf</c> (Brazilian CPF, 11 digits) or <c>cnpj</c> (Brazilian CNPJ, 14 digits), each with
///     two check digits. Mirrors the Java <c>Mod11Validator</c>.
/// </summary>
public class Mod11Validator : ISpanValidator
{
    /// <summary>The supported mod-11 schemes.</summary>
    public enum Variant
    {
        /// <summary>Brazilian CPF.</summary>
        Cpf,

        /// <summary>Brazilian CNPJ.</summary>
        Cnpj
    }

    private readonly Variant _variant;

    private Mod11Validator(Variant variant) => _variant = variant;

    /// <summary>Creates a validator from the policy params; requires a <c>variant</c> of cpf or cnpj.</summary>
    public static ISpanValidator FromParams(IReadOnlyDictionary<string, JsonElement>? parameters)
    {
        var variant = ValidatorParams.GetString(parameters, "variant");
        if (variant == null)
            throw new ArgumentException("The mod11 validator requires a 'variant' parameter (cpf or cnpj).");

        return variant.ToLowerInvariant() switch
        {
            "cpf" => new Mod11Validator(Variant.Cpf),
            "cnpj" => new Mod11Validator(Variant.Cnpj),
            _ => throw new ArgumentException($"Unsupported mod11 variant '{variant}'. Supported: cpf, cnpj.")
        };
    }

    /// <inheritdoc />
    public bool Validate(Span span) => _variant == Variant.Cpf ? IsValidCpf(span.Text) : IsValidCnpj(span.Text);

    /// <summary>Validates a Brazilian CPF (11 digits, two mod-11 check digits).</summary>
    public static bool IsValidCpf(string? text)
    {
        var d = DigitsOnly(text);
        if (d.Length != 11 || AllSameDigit(d))
            return false;

        var c1 = CheckDigit(d, 9, 10);
        var c2 = CheckDigit(d, 10, 11);
        return c1 == d[9] - '0' && c2 == d[10] - '0';
    }

    /// <summary>Validates a Brazilian CNPJ (14 digits, two mod-11 check digits).</summary>
    public static bool IsValidCnpj(string? text)
    {
        var d = DigitsOnly(text);
        if (d.Length != 14 || AllSameDigit(d))
            return false;

        int[] weights1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] weights2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        var c1 = CheckDigit(d, weights1, 12);
        var c2 = CheckDigit(d, weights2, 13);
        return c1 == d[12] - '0' && c2 == d[13] - '0';
    }

    private static int CheckDigit(string d, int length, int startWeight)
    {
        var sum = 0;
        for (var i = 0; i < length; i++)
            sum += (d[i] - '0') * (startWeight - i);
        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    private static int CheckDigit(string d, int[] weights, int length)
    {
        var sum = 0;
        for (var i = 0; i < length; i++)
            sum += (d[i] - '0') * weights[i];
        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    private static string DigitsOnly(string? text) =>
        text == null ? string.Empty : new string(text.Where(c => c is >= '0' and <= '9').ToArray());

    private static bool AllSameDigit(string d) => d.All(c => c == d[0]);
}
