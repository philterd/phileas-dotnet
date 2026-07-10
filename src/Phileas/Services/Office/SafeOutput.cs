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

namespace Phileas.Services.Office;

/// <summary>
///     Reads an Open XML package into an editable buffer and writes the redacted output once from an
///     in-memory buffer, deleting any partial output on failure. Redactors build the whole document
///     first, so the destination is never created until correct, complete bytes exist — never the
///     original or a half-written file.
/// </summary>
internal static class SafeOutput
{
    /// <summary>Writes <paramref name="bytes"/>, removing a partial file on failure.</summary>
    public static void Write(string path, byte[] bytes)
    {
        try
        {
            File.WriteAllBytes(path, bytes);
        }
        catch
        {
            TryDelete(path);
            throw;
        }
    }

    /// <summary>Reads a file into an expandable, writable stream (positioned at 0) for editing an Open XML package.</summary>
    public static MemoryStream ReadToEditableStream(string inputPath)
    {
        var stream = new MemoryStream();
        using (FileStream input = File.OpenRead(inputPath))
        {
            input.CopyTo(stream);
        }
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Copies <paramref name="bytes"/> into an expandable, writable stream (positioned at 0). A
    /// <see cref="MemoryStream"/> constructed directly over a byte array is fixed-length and can't grow, so
    /// editing an Open XML package in it fails; this copies into a resizable buffer instead.
    /// </summary>
    public static MemoryStream ToEditableStream(byte[] bytes)
    {
        var stream = new MemoryStream();
        stream.Write(bytes, 0, bytes.Length);
        stream.Position = 0;
        return stream;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best effort
        }
    }
}
