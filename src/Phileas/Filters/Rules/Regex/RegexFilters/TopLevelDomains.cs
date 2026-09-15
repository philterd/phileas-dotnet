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

namespace Phileas.Filters.Rules.Regex.RegexFilters;

/// <summary>
///     The IANA top-level domain list, bundled as an embedded resource, used by the email filter's
///     <c>onlyValidTLDs</c> option. The list is a point-in-time snapshot, so a newly delegated TLD is
///     not recognized until the bundled file is refreshed; that is the trade the option makes for
///     rejecting addresses on domains that cannot exist.
/// </summary>
internal static class TopLevelDomains
{
    private static readonly Lazy<HashSet<string>> Registered = new(Load);

    /// <summary>Returns whether the address's top-level domain is an IANA-registered one.</summary>
    /// <param name="emailAddress">The matched address.</param>
    public static bool IsRegistered(string emailAddress)
    {
        var at = emailAddress.LastIndexOf('@');
        if (at < 0)
            return false;

        var dot = emailAddress.LastIndexOf('.');
        if (dot < at || dot == emailAddress.Length - 1)
            return false;

        return Registered.Value.Contains(emailAddress[(dot + 1)..]);
    }

    private static HashSet<string> Load()
    {
        var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var assembly = typeof(TopLevelDomains).Assembly;
        var manifestName = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith(".tlds-alpha-by-domain.txt", StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(manifestName)!;
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            domains.Add(trimmed);
        }

        return domains;
    }
}
