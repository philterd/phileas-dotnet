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

using System.Text.RegularExpressions;

namespace Phileas.Model;

/// <summary>
///     Specifies the spatial relationship between a character sequence and a detected entity
///     that is used when adjusting a match's confidence score.
/// </summary>
public enum ConfidenceCondition
{
    /// <summary>The character sequence appears immediately before the entity.</summary>
    CharacterSequenceBefore,

    /// <summary>The character sequence appears immediately after the entity.</summary>
    CharacterSequenceAfter,

    /// <summary>The character sequence surrounds the entity (both before and after).</summary>
    CharacterSequenceSurrounding,

    /// <summary>A regular expression matches the text surrounding the entity.</summary>
    CharacterRegexSurrounding
}

/// <summary>
///     Adjusts the confidence score of a match based on surrounding character context.
/// </summary>
public class ConfidenceModifier
{
    /// <summary>Gets or sets the condition that triggers the confidence adjustment.</summary>
    public ConfidenceCondition Condition { get; set; }

    /// <summary>Gets or sets the character sequence used for comparison.</summary>
    public string Characters { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount added to (or subtracted from) the base confidence when the condition is met.</summary>
    public double ConfidenceDelta { get; set; }

    /// <summary>
    ///     Gets or sets an absolute confidence value applied when the condition is met (overrides
    ///     <see cref="ConfidenceDelta" /> when non-zero).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    ///     Gets or sets the optional regular expression used when <see cref="Condition" /> is
    ///     <see cref="ConfidenceCondition.CharacterRegexSurrounding" />.
    /// </summary>
    public Regex? MatchingPattern { get; set; }
}