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

using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Phileas.Model;

namespace Phileas.Services.Validators;

/// <summary>
///     Control-key validator based on a value mod 97. Variants: <c>nir</c> (French INSEE/NIR, with
///     Corsica substitutions) and <c>iban</c> (ISO 13616 MOD-97-10). Mirrors the Java
///     <c>Mod97Validator</c>.
/// </summary>
public partial class Mod97Validator : ISpanValidator
{
    /// <summary>The default Corsica department substitutions for the NIR.</summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultNirSubstitutions =
        new Dictionary<string, string> { ["2A"] = "19", ["2B"] = "18" };

    private enum Variant
    {
        Nir,
        Iban
    }

    private readonly Variant _variant;
    private readonly IReadOnlyDictionary<string, string> _substitutions;

    private Mod97Validator(Variant variant, IReadOnlyDictionary<string, string> substitutions)
    {
        _variant = variant;
        _substitutions = substitutions;
    }

    /// <summary>Creates a validator from the policy params; requires a <c>variant</c> of nir or iban.</summary>
    public static ISpanValidator FromParams(IReadOnlyDictionary<string, JsonElement>? parameters)
    {
        var variant = ValidatorParams.GetString(parameters, "variant");
        if (variant == null)
            throw new ArgumentException("The mod97 validator requires a 'variant' parameter (nir or iban).");

        return variant.ToLowerInvariant() switch
        {
            "nir" => new Mod97Validator(Variant.Nir,
                ValidatorParams.GetStringMap(parameters, "substitutions", DefaultNirSubstitutions)),
            "iban" => new Mod97Validator(Variant.Iban, new Dictionary<string, string>()),
            _ => throw new ArgumentException($"Unsupported mod97 variant '{variant}'. Supported: nir, iban.")
        };
    }

    /// <inheritdoc />
    public bool Validate(Span span) =>
        _variant == Variant.Nir ? IsValidNir(span.Text, _substitutions) : IsValidIban(span.Text);

    /// <summary>Validates a French NIR using the given Corsica letter substitutions.</summary>
    public static bool IsValidNir(string? text, IReadOnlyDictionary<string, string> substitutions)
    {
        if (text == null)
            return false;

        var s = WhitespaceRegex().Replace(text, string.Empty).ToUpperInvariant();
        if (s.Length != 15)
            return false;

        var body = s[..13];
        var key = s[13..];
        if (key.Length != 2 || !key.All(c => c is >= '0' and <= '9'))
            return false;

        foreach (var (k, v) in substitutions)
            body = body.Replace(k, v);

        if (body.Length != 13 || !body.All(c => c is >= '0' and <= '9'))
            return false;

        var n = long.Parse(body);
        var expectedKey = 97 - (int)(n % 97);
        return expectedKey == int.Parse(key);
    }

    /// <summary>Validates an IBAN by the MOD-97-10 rule (value mod 97 equals 1).</summary>
    public static bool IsValidIban(string? text)
    {
        if (text == null)
            return false;

        var s = WhitespaceRegex().Replace(text, string.Empty).ToUpperInvariant();
        if (!IbanRegex().IsMatch(s) || s.Length < 5 || s.Length > 34)
            return false;

        var rearranged = s[4..] + s[..4];
        var numeric = new System.Text.StringBuilder();
        foreach (var c in rearranged)
            if (c is >= '0' and <= '9')
                numeric.Append(c);
            else
                numeric.Append(10 + (c - 'A'));

        return BigInteger.Parse(numeric.ToString()) % 97 == 1;
    }

    [GeneratedRegex(@"\s")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^[A-Z]{2}[0-9]{2}[A-Z0-9]+$")]
    private static partial Regex IbanRegex();
}
