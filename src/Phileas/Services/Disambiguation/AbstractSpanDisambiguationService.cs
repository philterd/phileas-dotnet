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

using System.Text;
using Phileas.Services.Disambiguation.Vector;
using Phileas.Utils;

namespace Phileas.Services.Disambiguation;

/// <summary>
///     Base for vector-based disambiguation: holds the vector size, stop-word handling, and the token
///     hashing used to map context words to vector indexes.
///     <para>
///         The vector size factors into the hash function, so it cannot be grown over time: changing it
///         would invalidate every previously-accumulated value.
///     </para>
/// </summary>
public abstract class AbstractSpanDisambiguationService
{
    /// <summary>The vector (hash-table) size.</summary>
    protected readonly int VectorSize;

    /// <summary>Whether stop words are excluded from vectors.</summary>
    protected readonly bool IgnoreStopWords;

    /// <summary>The lower-cased stop-word set.</summary>
    protected readonly HashSet<string> StopWords;

    /// <summary>The vector store.</summary>
    protected readonly IVectorService VectorService;

    private readonly string _hashAlgorithm;

    /// <summary>Creates the base service from the given options and vector store.</summary>
    protected AbstractSpanDisambiguationService(SpanDisambiguationOptions options, IVectorService vectorService)
    {
        VectorSize = options.VectorSize;
        IgnoreStopWords = options.IgnoreStopWords;
        StopWords = ParseStopWords(options.StopWords);
        _hashAlgorithm = options.HashAlgorithm;
        VectorService = vectorService;
    }

    /// <summary>
    ///     Hashes a token to a vector index in <c>[0, VectorSize)</c>. Uses MurmurHash3 over the token's
    ///     UTF-8 bytes when configured (the default), so a token maps to the same index on every platform
    ///     and across runs; otherwise falls back to the Java string hash for deterministic parity.
    /// </summary>
    public int HashToken(string token)
    {
        if (_hashAlgorithm.Equals("murmur3", StringComparison.OrdinalIgnoreCase))
            return Math.Abs(MurmurHash3.Hash32X86(Encoding.UTF8.GetBytes(token)) % VectorSize);

        return Math.Abs(JavaStringHashCode(token) % VectorSize);
    }

    /// <summary>
    ///     Parses the comma-separated stop-word list into a set of individual, lower-cased words. Tokens
    ///     are compared against this set after being lower-cased, so the entries are stored lower-cased.
    /// </summary>
    private static HashSet<string> ParseStopWords(string? stopWords)
    {
        var words = new HashSet<string>();

        if (string.IsNullOrWhiteSpace(stopWords))
            return words;

        foreach (var word in stopWords.Split(','))
        {
            var trimmed = word.Trim().ToLowerInvariant();
            if (trimmed.Length > 0)
                words.Add(trimmed);
        }

        return words;
    }

    /// <summary>
    ///     Computes Java's <c>String.hashCode()</c> (<c>s[0]*31^(n-1) + ... + s[n-1]</c>) so the non-murmur3
    ///     fallback is deterministic and matches Java, rather than relying on .NET's per-process randomized
    ///     <see cref="string.GetHashCode()" />.
    /// </summary>
    private static int JavaStringHashCode(string token)
    {
        unchecked
        {
            var hash = 0;
            foreach (var c in token)
                hash = 31 * hash + c;
            return hash;
        }
    }
}
