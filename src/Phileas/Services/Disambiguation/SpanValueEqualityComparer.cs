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

namespace Phileas.Services.Disambiguation;

/// <summary>
///     Value equality for <see cref="Span" /> mirroring the Java <c>Span.equals</c> (transient fields such
///     as window, pattern, and always-valid are excluded). Used to dedupe spans that become identical once
///     their filter type is resolved. PDF geometry / line / page / paragraph fields from the Java model are
///     not present in this port and so are not compared.
/// </summary>
public sealed class SpanValueEqualityComparer : IEqualityComparer<Span>
{
    /// <summary>The shared instance.</summary>
    public static readonly SpanValueEqualityComparer Instance = new();

    /// <inheritdoc />
    public bool Equals(Span? x, Span? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        return x.CharacterStart == y.CharacterStart
               && x.CharacterEnd == y.CharacterEnd
               && x.Confidence.Equals(y.Confidence)
               && x.Ignored == y.Ignored
               && x.Applied == y.Applied
               && x.Priority == y.Priority
               && x.FilterType == y.FilterType
               && x.Context == y.Context
               && x.Text == y.Text
               && x.Replacement == y.Replacement
               && x.Salt == y.Salt
               && x.Classification == y.Classification;
    }

    /// <inheritdoc />
    public int GetHashCode(Span span)
    {
        var hash = new HashCode();
        hash.Add(span.CharacterStart);
        hash.Add(span.CharacterEnd);
        hash.Add(span.Confidence);
        hash.Add(span.Ignored);
        hash.Add(span.Applied);
        hash.Add(span.Priority);
        hash.Add(span.FilterType);
        hash.Add(span.Context);
        hash.Add(span.Text);
        hash.Add(span.Replacement);
        hash.Add(span.Salt);
        hash.Add(span.Classification);
        return hash.ToHashCode();
    }
}
