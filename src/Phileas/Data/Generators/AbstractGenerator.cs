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

using System.Reflection;

namespace Phileas.Data.Generators;

/// <summary>
///     Base class for generators that load their value pool from an embedded resource file. Mirrors the
///     Java <c>AbstractGenerator</c>; the resource is read from the assembly manifest (the .NET analog of
///     a classpath resource).
/// </summary>
/// <typeparam name="T">The type of value produced.</typeparam>
public abstract class AbstractGenerator<T> : IGenerator<T>
{
    /// <inheritdoc />
    public abstract T Random();

    /// <inheritdoc />
    public abstract long PoolSize();

    /// <summary>
    ///     Loads the non-empty, trimmed lines of an embedded resource (e.g. <c>"/first-names.txt"</c>).
    /// </summary>
    /// <param name="resourcePath">The resource path, with or without a leading slash.</param>
    /// <returns>The list of names.</returns>
    /// <exception cref="IOException">If the resource cannot be found.</exception>
    protected List<string> LoadNames(string resourcePath)
    {
        var fileName = resourcePath.TrimStart('/');
        var assembly = typeof(AbstractGenerator<>).Assembly;
        var manifestName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("." + fileName, StringComparison.Ordinal)
                                 || string.Equals(n, fileName, StringComparison.Ordinal));

        if (manifestName == null)
        {
            throw new IOException("Resource not found: " + resourcePath);
        }

        using var stream = assembly.GetManifestResourceStream(manifestName)
                           ?? throw new IOException("Resource not found: " + resourcePath);
        using var reader = new StreamReader(stream);

        var names = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.Length > 0)
            {
                names.Add(line);
            }
        }

        return names;
    }
}
