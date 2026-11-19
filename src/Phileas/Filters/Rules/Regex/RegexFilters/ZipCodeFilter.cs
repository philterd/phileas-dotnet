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
using Phileas.Model.Metadata;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Filters.Rules.Regex.RegexFilters;

/// <summary>
///     Regex-based filter that detects US ZIP code entities in plain text.
/// </summary>
public class ZipCodeFilter : RegexFilter
{
    // With a required delimiter the +4 extension must be dash-separated and the match is high confidence;
    // without it the extension may be undelimited and the match is lower confidence. Mirrors Java's ZipCodeFilter.
    /// <summary>Words near a match that raise its confidence; the Java port's list.</summary>
    private static readonly HashSet<string> ContextualTerms = new() { "zip", "zipcode", "postal" };

    // Five digits joined by a hyphen to a letter or digit before them, as in a case number like 2026-00847
    // or a reference code like FR-48213, are part of a longer identifier and not a ZIP code (#193, as
    // philterd/phileas#485). A standalone five-digit number before the hyphen is still allowed, so both
    // ends of a range like 02116-02118 are found.
    private const string NotAfterHyphen =
        @"(?:(?<![\p{L}\p{N}][" + IdentifierSeparators.JoiningHyphens + "])"
        + @"|(?<=(?:^|[^" + IdentifierSeparators.JoiningHyphens + @"\p{L}\p{N}])[0-9]{5}["
        + IdentifierSeparators.JoiningHyphens + "]))";

    private static readonly Analyzer DelimitedAnalyzer = new(ContextualTerms,
        new FilterPattern.Builder().WithPattern(NotAfterHyphen + @"\b[0-9]{5}(?:-[0-9]{4})?\b")
            .WithInitialConfidence(0.90).Build()
    );

    private static readonly Analyzer UndelimitedAnalyzer = new(ContextualTerms,
        new FilterPattern.Builder().WithPattern(NotAfterHyphen + @"\b[0-9]{5}(?:-?[0-9]{4})?\b")
            .WithInitialConfidence(0.50).Build()
    );

    private static readonly ZipCodeMetadataService ZipCodeMetadata = new();

    private readonly Analyzer _analyzer;
    private readonly bool _validate;

    /// <summary>
    ///     Initializes a new <see cref="ZipCodeFilter" /> with the given configuration.
    /// </summary>
    /// <param name="configuration">Runtime filter configuration.</param>
    /// <param name="requireDelimiter">When <see langword="true" />, the +4 extension must be dash-separated.</param>
    /// <param name="validate">When <see langword="true" />, ZIP codes not in the census database are not redacted.</param>
    public ZipCodeFilter(FilterConfiguration configuration, bool requireDelimiter = false, bool validate = false)
        : base(FilterType.ZipCode, configuration)
    {
        _analyzer = requireDelimiter ? DelimitedAnalyzer : UndelimitedAnalyzer;
        _validate = validate;
    }

    /// <inheritdoc />
    public override Filtered Filter(PhileasPolicy policy, string context, int piece, string input)
    {
        var spans = FindSpans(policy, _analyzer, input, context, piece);
        spans = PostFilter(spans, input);

        if (_validate)
            // The census database only stores the first five digits, so validate against those.
            spans = spans.Where(span => ZipCodeMetadata.GetMetadata(span.Text[..5]).Exists).ToList();

        spans = Span.DropOverlappingSpans(spans);
        return new Filtered(context, piece, spans);
    }
}