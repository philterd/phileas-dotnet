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
using System.Text.RegularExpressions;

namespace Phileas.Services.Split;

/// <summary>
///     Splits a document into chunks of at most a fixed number of characters, keeping whole sentences
///     together where possible.
/// </summary>
public class CharacterCountSplitService : AbstractSplitService, ISplitService
{
    private static readonly Regex SentenceBoundary = new(@"(?<=[.?!])\s");

    private readonly int _maxChunkSize;

    /// <summary>Creates a splitter producing chunks of at most <paramref name="maxChunkSize" /> characters.</summary>
    public CharacterCountSplitService(int maxChunkSize)
    {
        _maxChunkSize = maxChunkSize;
    }

    /// <inheritdoc />
    public List<string> Split(string input)
    {
        var splits = new List<string>();
        if (string.IsNullOrWhiteSpace(input)) return splits;

        // Split the text into sentences, keeping the sentence-ending punctuation with the sentence.
        var sentences = SentenceBoundary.Split(input);
        var currentChunk = new StringBuilder();

        foreach (var sentence in sentences)
        {
            // If adding the next sentence would exceed the max chunk size, and the current chunk is not
            // empty, finalize the current chunk.
            if (currentChunk.Length > 0 && currentChunk.Length + sentence.Length > _maxChunkSize)
            {
                splits.Add(currentChunk.ToString().Trim());
                currentChunk = new StringBuilder();
            }

            currentChunk.Append(sentence).Append(' ');
        }

        if (currentChunk.Length > 0)
        {
            splits.Add(currentChunk.ToString().Trim());
        }

        return splits;
    }

    /// <inheritdoc />
    public string GetSeparator() => " ";
}
