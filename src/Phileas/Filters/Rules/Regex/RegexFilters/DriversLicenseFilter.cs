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

using Phileas.Model;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Rules.Regex.RegexFilters;

/// <summary>
///     Regex-based filter that detects driver's license number entities in plain text.
/// </summary>
public class DriversLicenseFilter : RegexFilter
{
    /// <summary>Words near a match that raise its confidence; the Java port's list.</summary>
    private static readonly HashSet<string> ContextualTerms = new() { "license", "licence", "lic", "driver", "drivers" };
    private static readonly HashSet<string> CapitalisedTerms = new() { "DL", "DLN" };

    private static readonly Analyzer DriversLicenseAnalyzer = new(ContextualTerms,
        new FilterPattern.Builder().WithPattern(@"\b[A-Z][0-9]{7}\b").WithInitialConfidence(0.50).Build(),
        new FilterPattern.Builder().WithPattern(@"\b[A-Z]{2}[0-9]{6}\b").WithInitialConfidence(0.50).Build(),
        // 1 letter + 12 digits (13-char): Florida, Maryland, and Michigan-style license numbers.
        new FilterPattern.Builder().WithPattern(@"\b[A-Z][0-9]{12}\b").WithInitialConfidence(0.50).Build(),
        new FilterPattern.Builder().WithPattern(@"\b[0-9]{9}\b").WithInitialConfidence(0.50).Build(),

        // ALABAMA + HAWAII + SOUTH DAKOTA + TEXAS
        new FilterPattern.Builder().WithPattern(@"\b[\d]{8}\b").WithInitialConfidence(0.50).Build(),
        // ALASKA + DELAWARE + DISTRICT OF COLUMBIA + OREGON + RHODE ISLAND
        new FilterPattern.Builder().WithPattern(@"\b[\d]{7}\b").WithInitialConfidence(0.50).Build(),
        // ARIZONA
        new FilterPattern.Builder().WithPattern(@"\b(?:[A-Z][\d]{8}|[\d]{9})\b").WithInitialConfidence(0.50).Build(),
        // ARKANSAS
        new FilterPattern.Builder().WithPattern(@"\b[9][\d]{7}\b").WithInitialConfidence(0.50).Build(),
        // CALIFORNIA
        new FilterPattern.Builder().WithPattern(@"\b[A-Z][\d]{7}\b").WithInitialConfidence(0.50).Build(),
        // COLORADO
        new FilterPattern.Builder().WithPattern(@"\b[\d]{2}-[\d]{3}[\d]{4}\b").WithInitialConfidence(0.50).Build(),
        // CONNECTICUT + GEORGIA + MISSISSIPPI + NEW MEXICO + SOUTH CAROLINA
        new FilterPattern.Builder().WithPattern(@"\b[\d]{9}\b").WithInitialConfidence(0.50).Build(),
        // FLORIDA
        new FilterPattern.Builder().WithPattern(@"\b[A-Z][\d]{3}-[\d]{3}-[\d]{2}-[\d]{3}\b").WithInitialConfidence(0.50).Build(),
        // IDAHO
        new FilterPattern.Builder().WithPattern(@"\b[A-Z]{2}[\d]{6}[A-Z]\b").WithInitialConfidence(0.50).Build(),
        // ILLINOIS
        new FilterPattern.Builder().WithPattern(@"\b[A-Z][\d]{11}\b").WithInitialConfidence(0.50).Build(),
        // INDIANA
        new FilterPattern.Builder().WithPattern(@"\b[\d]{4}-[\d]{2}-[\d]{4}\b").WithInitialConfidence(0.50).Build(),
        // IOWA
        new FilterPattern.Builder().WithPattern(@"\b(?:[\d]{9}|[\d]{3}[A-Z]{2}[\d]{4})\b").WithInitialConfidence(0.50).Build(),
        // KANSAS
        new FilterPattern.Builder().WithPattern(@"\b[Kk][\d]{2}-[\d]{2}-[\d]{4}\b").WithInitialConfidence(0.50).Build(),
        // KENTUCKY
        new FilterPattern.Builder().WithPattern(@"\b[A-Z][\d]{2}-[\d]{3}-[\d]{3}\b").WithInitialConfidence(0.50).Build(),
        // LOUISIANA
        new FilterPattern.Builder().WithPattern(@"\b[00][\d]{7}\b").WithInitialConfidence(0.50).Build(),
        // MAINE
        new FilterPattern.Builder().WithPattern(@"\b(?:[\d]{7}|[\d]{7}[Xx])\b").WithInitialConfidence(0.50).Build(),
        // MARYLAND
        new FilterPattern.Builder().WithPattern(@"\b[A-Z]-[\d]{3}-[\d]{3}-[\d]{3}-[\d]{3}\b").WithInitialConfidence(0.50).Build(),
        // MASSACHUSETTS
        new FilterPattern.Builder().WithPattern(@"\b[Ss][1][\d]{8}\b").WithInitialConfidence(0.50).Build(),
        // MICHIGAN + MINNESOTA
        new FilterPattern.Builder().WithPattern(@"\b[A-Z][\d]{12}\b").WithInitialConfidence(0.50).Build(),
        // MISSOURI
        new FilterPattern.Builder().WithPattern(@"\b([A-Z][\d]{5,9})\b").WithInitialConfidence(0.50).Build(),
        // MONTANA
        new FilterPattern.Builder().WithPattern(@"\b[\d]{13}\b").WithInitialConfidence(0.50).Build(),
        // NEBRASKA
        new FilterPattern.Builder().WithPattern(@"\b[A-Z][\d]{3,8}\b").WithInitialConfidence(0.50).Build(),
        // NEVADA
        new FilterPattern.Builder().WithPattern(@"\b[\d]{10}\b").WithInitialConfidence(0.50).Build(),
        // NEW HAMPSHIRE
        new FilterPattern.Builder().WithPattern(@"\b[\d]{2}[A-Z]{3}[\d]{5}\b").WithInitialConfidence(0.50).Build(),
        // NEW JERSEY
        new FilterPattern.Builder().WithPattern(@"\b[A-Z][\d]{4}-[\d]{5}-[\d]{5}\b").WithInitialConfidence(0.50).Build(),
        // NEW YORK
        new FilterPattern.Builder().WithPattern(@"\b(?:[\d]{9}|[\d]{3}-[\d]{3}-[\d]{3})\b").WithInitialConfidence(0.50).Build(),
        // NORTH CAROLINA
        new FilterPattern.Builder().WithPattern(@"\b[\d]{8,12}\b").WithInitialConfidence(0.50).Build(),
        // NORTH DAKOTA
        new FilterPattern.Builder().WithPattern(@"\b(?:[A-Z]{3}[\d]{6}|[A-Z]{3}-[\d]{2}-[\d]{4})\b").WithInitialConfidence(0.50).Build(),
        // OHIO
        new FilterPattern.Builder().WithPattern(@"\b[A-Z]{2}[\d]{6}\b").WithInitialConfidence(0.50).Build(),
        // OKLAHOMA
        new FilterPattern.Builder().WithPattern(@"\b(?:[A-Z][\d]{8}|[A-Z][\d]{9})\b").WithInitialConfidence(0.50).Build(),
        // PENNSYLVANIA
        new FilterPattern.Builder().WithPattern(@"\b(?:[\d]{8}|[\d]{2}[ ][\d]{3}[ ][\d]{3})\b").WithInitialConfidence(0.50).Build(),
        // TENNESSEE
        new FilterPattern.Builder().WithPattern(@"\b[\d]{8,9}\b").WithInitialConfidence(0.50).Build(),
        // UTAH
        new FilterPattern.Builder().WithPattern(@"\b[\d]{4,10}\b").WithInitialConfidence(0.50).Build(),
        // VERMONT
        new FilterPattern.Builder().WithPattern(@"\b(?:[\d]{8}|[\d]{7}[A-Z])\b").WithInitialConfidence(0.50).Build(),
        // VIRGINIA
        new FilterPattern.Builder().WithPattern(@"\b[A-Z][\d]{2}-[\d]{2}-[\d]{4}\b").WithInitialConfidence(0.50).Build(),
        // WASHINGTON
        new FilterPattern.Builder().WithPattern(@"\b[A-Z\d]{12}\b").WithInitialConfidence(0.50).Build(),
        // WEST VIRGINIA
        new FilterPattern.Builder().WithPattern(@"\b(?:[\d]{7}|[A-Z][\d]{6})\b").WithInitialConfidence(0.50).Build(),
        // WISCONSIN
        new FilterPattern.Builder().WithPattern(@"\b[A-Z][\d]{13}\b").WithInitialConfidence(0.50).Build(),
        // WYOMING
        new FilterPattern.Builder().WithPattern(@"\b(?:[\d]{9}|[\d]{6}-[\d]{3})\b").WithInitialConfidence(0.50).Build()
    );

    /// <summary>
    ///     Initializes a new <see cref="DriversLicenseFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    public DriversLicenseFilter(FilterConfiguration configuration) : base(FilterType.DriversLicenseNumber,
        configuration)
    {
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, DriversLicenseAnalyzer, input, context, piece);
        spans = PostFilter(spans, input);
        spans = Span.DropOverlappingSpans(spans);

        var spansToRemove = new List<Span>();
        foreach (Span span in spans)
        {
            if (!span.Text.Any(char.IsLetter) && !HasContextualTerm(span.Window))
                spansToRemove.Add(span);
        }

        foreach (Span span in spansToRemove)
        {
            spans.Remove(span);
        }

        return new Filtered(context, piece, spans);
    }

    private bool HasContextualTerm(string[] window)
    {
        if (window == null || window.Length == 0)
            return false;

        foreach (string word in window)
        {
            if (CapitalisedTerms.Contains(RemovePunctuation(word)) || ContextualTerms.Contains(RemovePunctuation(word).ToLower()))
                return true;
        }

        return false;
    }

    private string RemovePunctuation(string word)
    {
        word = word.Replace(" ", string.Empty);
        word = word.Replace(":", string.Empty);
        word = word.Replace(".", string.Empty);
        word = word.Replace("#", string.Empty);
        word = word.Replace("'", string.Empty);
        return word;
    }
}