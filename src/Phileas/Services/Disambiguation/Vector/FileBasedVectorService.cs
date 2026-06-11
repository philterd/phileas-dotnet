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

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Phileas.Model;

namespace Phileas.Services.Disambiguation.Vector;

/// <summary>
///     A <see cref="IVectorService" /> that persists the accumulated disambiguation vectors to a file so
///     the learning survives process restarts. The vectors are held in memory (inheriting the thread-safe
///     accumulation of <see cref="InMemoryVectorService" />) and loaded from the file on construction.
///     <para>
///         Because writing the whole store on every token insert would dominate throughput, persistence is
///         explicit: call <see cref="Save" /> on whatever cadence fits, or use a <c>using</c> block /
///         <see cref="Dispose" /> to save once at the end.
///     </para>
///     <para>
///         Stored vectors are only meaningful for the exact vector size and hash algorithm they were built
///         with, so the file records both; a load whose parameters do not match (or whose format version is
///         unknown) is discarded with a warning and treated as a cold start.
///     </para>
/// </summary>
public class FileBasedVectorService : InMemoryVectorService, IDisposable
{
    /// <summary>Bumped if the on-disk layout changes incompatibly.</summary>
    private const int FormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _path;
    private readonly int _vectorSize;
    private readonly string _hashAlgorithm;

    /// <summary>
    ///     Creates the service backed by the given file, loading any previously-saved vectors that were
    ///     built with the same vector size and hash algorithm.
    /// </summary>
    /// <param name="path">The file used to persist and restore the vectors.</param>
    /// <param name="vectorSize">The configured disambiguation vector size.</param>
    /// <param name="hashAlgorithm">The configured hash algorithm.</param>
    public FileBasedVectorService(string path, int vectorSize, string hashAlgorithm)
    {
        _path = path;
        _vectorSize = vectorSize;
        _hashAlgorithm = NormalizeAlgorithm(hashAlgorithm);
        Load();
    }

    /// <summary>Saves the vectors. Allows the service to be used in a <c>using</c> block.</summary>
    public void Dispose()
    {
        Save();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Normalizes the configured algorithm name to its effective behavior. Anything that is not
    ///     (case-insensitively) <c>murmur3</c> falls back to <c>hashCode</c>, mirroring
    ///     <see cref="AbstractSpanDisambiguationService" />, so two names that mean the same thing are not
    ///     treated as a mismatch.
    /// </summary>
    private static string NormalizeAlgorithm(string? hashAlgorithm)
    {
        return hashAlgorithm != null && hashAlgorithm.Equals("murmur3", StringComparison.OrdinalIgnoreCase)
            ? "murmur3"
            : "hashCode";
    }

    /// <summary>
    ///     Loads the persisted vectors into the in-memory cache. A missing or empty file is a cold start,
    ///     and a file whose parameters do not match the configured ones is discarded (also a cold start).
    /// </summary>
    private void Load()
    {
        if (string.IsNullOrEmpty(_path) || !File.Exists(_path) || new FileInfo(_path).Length == 0)
            return;

        var json = File.ReadAllText(_path);
        var persisted = JsonSerializer.Deserialize<PersistedVectors>(json, JsonOptions);

        if (persisted?.Vectors == null)
            return;

        // Refuse to load vectors built with a different size/algorithm/format: their index values would be
        // meaningless under the current configuration.
        var persistedAlgorithm = NormalizeAlgorithm(persisted.HashAlgorithm);
        if (persisted.Version != FormatVersion
            || persisted.VectorSize != _vectorSize
            || persistedAlgorithm != _hashAlgorithm)
            return;

        foreach (var (context, filterTypeMap) in persisted.Vectors)
        {
            // Start from a fully-populated context (all filter types present) so reads are never null.
            var contextVectors = new Dictionary<FilterType, SpanVector>();
            foreach (FilterType filterType in Enum.GetValues<FilterType>())
                contextVectors[filterType] = new SpanVector();

            foreach (var (filterTypeName, indexes) in filterTypeMap)
            {
                if (!Enum.TryParse<FilterType>(filterTypeName, out var filterType) || indexes == null)
                    continue;

                var rebuilt = new ConcurrentDictionary<double, double>();
                foreach (var (indexKey, count) in indexes)
                    rebuilt[double.Parse(indexKey, CultureInfo.InvariantCulture)] = count;
                contextVectors[filterType].VectorIndexes = rebuilt;
            }

            VectorCache[context] = contextVectors;
        }
    }

    /// <summary>
    ///     Persists the current vectors to the file, recording the vector size and hash algorithm they were
    ///     built with. The write goes to a temporary file that is then moved into place, so a crash mid-write
    ///     cannot corrupt an existing store.
    /// </summary>
    public void Save()
    {
        var vectors = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();

        foreach (var (context, filterTypeMap) in VectorCache)
        {
            var contextSnapshot = new Dictionary<string, Dictionary<string, double>>();

            foreach (var (filterType, spanVector) in filterTypeMap)
            {
                var indexes = spanVector.VectorIndexes;

                // Only persist filter types that actually accumulated something, to keep the file small.
                if (indexes.IsEmpty)
                    continue;

                var snapshot = new Dictionary<string, double>();
                foreach (var (index, count) in indexes)
                    snapshot[index.ToString(CultureInfo.InvariantCulture)] = count;
                contextSnapshot[filterType.ToString()] = snapshot;
            }

            if (contextSnapshot.Count > 0)
                vectors[context] = contextSnapshot;
        }

        var persisted = new PersistedVectors
        {
            Version = FormatVersion,
            VectorSize = _vectorSize,
            HashAlgorithm = _hashAlgorithm,
            Vectors = vectors
        };

        var directory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temp = _path + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(persisted, JsonOptions));
            File.Move(temp, _path, true);
        }
        catch
        {
            if (File.Exists(temp))
                File.Delete(temp);
            throw;
        }
    }

    /// <summary>The on-disk representation: the vectors plus the parameters they were built with.</summary>
    private sealed class PersistedVectors
    {
        public int Version { get; set; }
        public int VectorSize { get; set; }
        public string? HashAlgorithm { get; set; }
        public Dictionary<string, Dictionary<string, Dictionary<string, double>>>? Vectors { get; set; }
    }
}
