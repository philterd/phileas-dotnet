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
/// Describes a compiled regular expression together with the metadata used to score and
/// classify matches made by a <see cref="Phileas.Filters.Analyzer"/>.
/// Instances are constructed via the nested <see cref="Builder"/> class.
/// </summary>
public class FilterPattern
{
    /// <summary>Gets the compiled regular expression used to find matches in input text.</summary>
    public Regex Pattern { get; }

    /// <summary>Gets an optional display format string for the pattern.</summary>
    public string? Format { get; }

    /// <summary>Gets the baseline confidence score assigned to every match produced by this pattern.</summary>
    public double InitialConfidence { get; }

    /// <summary>Gets an optional classification label applied to all matches (e.g. "PERSON").</summary>
    public string? Classification { get; }

    /// <summary>Gets a value indicating whether matches from this pattern are always considered valid regardless of confidence.</summary>
    public bool AlwaysValid { get; }

    /// <summary>Gets the capture-group number whose value is used as the match text; 0 means the entire match.</summary>
    public int GroupNumber { get; }

    /// <summary>Gets the optional list of <see cref="ConfidenceModifier"/> rules that adjust the confidence of individual matches.</summary>
    public IList<ConfidenceModifier>? ConfidenceModifiers { get; }

    private FilterPattern(Builder builder)
    {
        Pattern = builder.Pattern ?? throw new ArgumentNullException(nameof(builder.Pattern));
        Format = builder.Format;
        InitialConfidence = builder.InitialConfidence;
        Classification = builder.Classification;
        AlwaysValid = builder.AlwaysValid;
        GroupNumber = builder.GroupNumber;
        ConfidenceModifiers = builder.ConfidenceModifiers;
    }

    /// <summary>
    /// Fluent builder for <see cref="FilterPattern"/>.
    /// </summary>
    public class Builder
    {
        internal Regex? Pattern;
        internal string? Format;
        internal double InitialConfidence = 0.9;
        internal string? Classification;
        internal bool AlwaysValid;
        internal int GroupNumber;
        internal IList<ConfidenceModifier>? ConfidenceModifiers;

        /// <summary>Sets the compiled pattern directly.</summary>
        /// <param name="pattern">A pre-compiled <see cref="Regex"/>.</param>
        public Builder WithPattern(Regex pattern) { Pattern = pattern; return this; }

        /// <summary>Compiles a pattern from the provided string, optionally combining it with additional <paramref name="options"/>.</summary>
        /// <param name="pattern">The regular expression source string.</param>
        /// <param name="options">Additional <see cref="RegexOptions"/> flags; <see cref="RegexOptions.Compiled"/> is always added.</param>
        public Builder WithPattern(string pattern, RegexOptions options = RegexOptions.None)
        {
            Pattern = new Regex(pattern, options | RegexOptions.Compiled);
            return this;
        }

        /// <summary>Sets a display format string for the pattern.</summary>
        public Builder WithFormat(string format) { Format = format; return this; }

        /// <summary>Sets the baseline confidence assigned to every match. Defaults to 0.9.</summary>
        public Builder WithInitialConfidence(double confidence) { InitialConfidence = confidence; return this; }

        /// <summary>Sets the classification label applied to all matches.</summary>
        public Builder WithClassification(string classification) { Classification = classification; return this; }

        /// <summary>Sets whether matches are always treated as valid regardless of confidence.</summary>
        public Builder WithAlwaysValid(bool alwaysValid) { AlwaysValid = alwaysValid; return this; }

        /// <summary>Sets the capture-group number whose value is used as the match text.</summary>
        public Builder WithGroupNumber(int groupNumber) { GroupNumber = groupNumber; return this; }

        /// <summary>Sets the confidence modifier rules that adjust per-match confidence scores.</summary>
        public Builder WithConfidenceModifiers(IList<ConfidenceModifier> modifiers) { ConfidenceModifiers = modifiers; return this; }

        /// <summary>Builds and returns the <see cref="FilterPattern"/>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when <see cref="Pattern"/> has not been set.</exception>
        public FilterPattern Build() => new FilterPattern(this);
    }
}
