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

using System.Text.RegularExpressions;
using Phileas.Model;

namespace Phileas.Services.Validators;

/// <summary>
///     Spanish CIF (organization tax identifier) control validator: a leading organization-type
///     letter, seven digits, and a control character that is a digit or a letter from
///     <c>JABCDEFGHI</c>, derived from a Luhn-like weighted sum. Mirrors the Java <c>EsCifValidator</c>.
/// </summary>
public partial class EsCifValidator : ISpanValidator
{
    private const string ValidFirst = "ABCDEFGHJNPQRSUVW";
    private const string ControlLetters = "JABCDEFGHI";

    private static readonly EsCifValidator Singleton = new();

    private EsCifValidator()
    {
    }

    /// <summary>Returns the shared instance.</summary>
    public static ISpanValidator GetInstance() => Singleton;

    /// <inheritdoc />
    public bool Validate(Span span) => IsValid(span.Text);

    /// <summary>Validates a Spanish CIF control character.</summary>
    public static bool IsValid(string? text)
    {
        if (text == null)
            return false;

        var s = SeparatorRegex().Replace(text.Trim().ToUpperInvariant(), string.Empty);
        if (s.Length != 9)
            return false;

        if (!ValidFirst.Contains(s[0]))
            return false;

        var middle = s.Substring(1, 7);
        if (!middle.All(c => c is >= '0' and <= '9'))
            return false;

        var sum = 0;
        for (var i = 0; i < 7; i++)
        {
            var digit = middle[i] - '0';
            if (i % 2 == 0)
            {
                var doubled = digit * 2;
                sum += doubled / 10 + doubled % 10;
            }
            else
            {
                sum += digit;
            }
        }

        var check = (10 - sum % 10) % 10;
        var control = s[8];

        if (control is >= '0' and <= '9')
            return control - '0' == check;

        return control == ControlLetters[check];
    }

    [GeneratedRegex(@"[\s-]")]
    private static partial Regex SeparatorRegex();
}
