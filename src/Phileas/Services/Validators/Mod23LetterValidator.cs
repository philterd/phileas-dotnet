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
using System.Text.RegularExpressions;
using Phileas.Model;

namespace Phileas.Services.Validators;

/// <summary>
///     Control-letter validator: the control letter is taken from a 23-entry table indexed by the
///     number mod 23. Validates the Spanish DNI and NIE (leading X/Y/Z mapped to 0/1/2). Mirrors the
///     Java <c>Mod23LetterValidator</c>.
/// </summary>
public partial class Mod23LetterValidator : ISpanValidator
{
    private const string ControlLetters = "TRWAGMYFPDXBNJZSQVHLCKE";

    /// <summary>The default NIE leading-letter substitutions.</summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultPrefixSubstitutions =
        new Dictionary<string, string> { ["X"] = "0", ["Y"] = "1", ["Z"] = "2" };

    private readonly IReadOnlyDictionary<string, string> _prefixSubstitutions;

    private Mod23LetterValidator(IReadOnlyDictionary<string, string> prefixSubstitutions) =>
        _prefixSubstitutions = prefixSubstitutions;

    /// <summary>Creates a validator from the policy params; substitutions default to the NIE mapping.</summary>
    public static ISpanValidator FromParams(IReadOnlyDictionary<string, JsonElement>? parameters) =>
        new Mod23LetterValidator(
            ValidatorParams.GetStringMap(parameters, "substitutions", DefaultPrefixSubstitutions));

    /// <inheritdoc />
    public bool Validate(Span span) => IsValid(span.Text, _prefixSubstitutions);

    /// <summary>Validates a Spanish DNI or NIE control letter.</summary>
    public static bool IsValid(string? text, IReadOnlyDictionary<string, string> prefixSubstitutions)
    {
        if (text == null)
            return false;

        var s = SeparatorRegex().Replace(text.Trim().ToUpperInvariant(), string.Empty);
        if (s.Length != 9)
            return false;

        var control = s[8];
        if (control is < 'A' or > 'Z')
            return false;

        string numberPart;
        var prefix = s[0].ToString();
        if (prefixSubstitutions.TryGetValue(prefix, out var substitute))
        {
            var rest = s.Substring(1, 7);
            if (!rest.All(c => c is >= '0' and <= '9'))
                return false;
            numberPart = substitute + rest;
        }
        else
        {
            numberPart = s[..8];
            if (!numberPart.All(c => c is >= '0' and <= '9'))
                return false;
        }

        var n = long.Parse(numberPart);
        return ControlLetters[(int)(n % 23)] == control;
    }

    [GeneratedRegex(@"[\s-]")]
    private static partial Regex SeparatorRegex();
}
