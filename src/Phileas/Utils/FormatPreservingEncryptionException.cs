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

namespace Phileas.Utils;

/// <summary>
///     Thrown when a value cannot be format-preserving encrypted or decrypted — for example because its
///     format-preservable content length is outside the range FF3 supports. The plain text is never
///     included in the message.
/// </summary>
public class FormatPreservingEncryptionException : Exception
{
    /// <summary>Creates a new <see cref="FormatPreservingEncryptionException" />.</summary>
    public FormatPreservingEncryptionException(string message) : base(message)
    {
    }

    /// <summary>Creates a new <see cref="FormatPreservingEncryptionException" /> with an underlying cause.</summary>
    public FormatPreservingEncryptionException(string message, Exception cause) : base(message, cause)
    {
    }
}
