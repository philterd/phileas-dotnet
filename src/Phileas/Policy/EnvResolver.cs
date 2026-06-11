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

namespace Phileas.Policy;

/// <summary>
///     Resolves <c>env:NAME</c> references to the value of the named environment variable. Used by
///     <see cref="Crypto" /> and <see cref="Fpe" /> so that PhiSQL-authored policies can keep encryption
///     secrets out of the policy document.
/// </summary>
internal static class EnvResolver
{
    private const string Prefix = "env:";

    /// <summary>
    ///     Returns the value of the environment variable named after the <c>env:</c> prefix, or the input
    ///     value unchanged when it is not an <c>env:</c> reference.
    /// </summary>
    public static string? Resolve(string? value)
    {
        return value != null && value.StartsWith(Prefix, StringComparison.Ordinal)
            ? Environment.GetEnvironmentVariable(value[Prefix.Length..])
            : value;
    }
}
