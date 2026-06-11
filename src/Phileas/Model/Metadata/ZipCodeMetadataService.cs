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

using System.Globalization;
using System.Reflection;

namespace Phileas.Model.Metadata;

/// <summary>
///     Looks up the census population for a US ZIP code from the bundled
///     <c>zip-code-population.csv</c>. Used by the <c>population</c> condition on ZIP code filters.
/// </summary>
public class ZipCodeMetadataService
{
    private readonly Dictionary<string, int> _zipCodesFromCensus = new();

    /// <summary>Creates the service, loading the bundled census data.</summary>
    public ZipCodeMetadataService()
    {
        var assembly = typeof(ZipCodeMetadataService).Assembly;
        var manifestName = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith(".zip-code-population.csv", StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(manifestName)!;
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith('#')) continue;
            var parts = line.Split(',');
            _zipCodesFromCensus[parts[0]] = int.Parse(parts[1], CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    ///     Returns the population for <paramref name="zipCode" />, or <c>(-1, false)</c> when the ZIP
    ///     code is not in the census data.
    /// </summary>
    public (int Population, bool Exists) GetMetadata(string zipCode)
    {
        return _zipCodesFromCensus.TryGetValue(zipCode, out var population)
            ? (population, true)
            : (-1, false);
    }
}
