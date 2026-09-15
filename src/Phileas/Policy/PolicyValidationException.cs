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
///     Thrown when a policy does not match the redaction policy schema. A policy carrying a key the
///     schema does not define, or a value it does not allow, previously loaded and then quietly did
///     not do what it said: the misspelled filter simply never ran.
/// </summary>
public class PolicyValidationException : Exception
{
    /// <summary>Creates the exception from the schema failures.</summary>
    /// <param name="errors">One entry per failure, as <c>location: reason</c>.</param>
    public PolicyValidationException(IReadOnlyList<string> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    /// <summary>Every schema failure, as <c>location: reason</c>.</summary>
    public IReadOnlyList<string> Errors { get; }

    private static string BuildMessage(IReadOnlyList<string> errors)
    {
        // Enough to find the problem without printing an unbounded wall of text for a policy that is
        // wrong in many places at once.
        const int listed = 10;
        var message = "The policy does not match the redaction policy schema ("
                      + PolicySchema.GetSupportedSchemaVersion() + "): "
                      + string.Join("; ", errors.Take(listed));

        return errors.Count > listed
            ? message + $"; and {errors.Count - listed} more"
            : message;
    }
}
