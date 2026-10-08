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

using Phileas.Model.Metadata;
using Phileas.Model;

namespace Phileas.Filters.Conditions;

/// <summary>
///     Evaluates strategy conditions parsed by <see cref="ConditionParser" />: one or more <c>field op value</c>
///     comparisons on population, token, type, confidence or context, joined with <c>and</c>. A condition that does
///     not parse throws <see cref="InvalidConditionException" />; it is never treated as satisfied. Policies are
///     checked when they are loaded, so a condition that does not parse is normally rejected before any text is
///     filtered.
/// </summary>
public static class ConditionEvaluator
{
    private static readonly ZipCodeMetadataService ZipCodeMetadata = new();

    /// <summary>
    ///     Evaluates the given condition string against the supplied runtime values.
    ///     Returns <see langword="true" /> when the condition is satisfied or when
    ///     <paramref name="condition" /> is <see langword="null" /> or whitespace.
    /// </summary>
    /// <param name="condition">The condition expression to evaluate, or <see langword="null" />.</param>
    /// <param name="context">The current context identifier.</param>
    /// <param name="token">The detected entity text.</param>
    /// <param name="confidence">The detection confidence score.</param>
    /// <param name="classification">Optional entity classification label.</param>
    /// <returns><see langword="true" /> if the condition passes; otherwise <see langword="false" />.</returns>
    /// <exception cref="InvalidConditionException">The condition does not parse.</exception>
    public static bool Evaluate(
        string condition,
        string context,
        string token,
        double confidence,
        string? classification)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return true;

        foreach (var clause in ConditionParser.Parse(condition))
        {
            var satisfied = clause.Field switch
            {
                "population" => EvaluatePopulation(clause, token),
                "token" => EvaluateText(clause, token),
                "type" => EvaluateType(clause, classification),
                "confidence" => EvaluateConfidence(clause, confidence),
                "context" => EvaluateText(clause, context),
                _ => throw Unreachable(clause)
            };
            if (!satisfied) return false;
        }

        return true;
    }

    private static bool EvaluatePopulation(ConditionClause clause, string token)
    {
        // The population condition applies to zip-code tokens: look up the census population for the
        // token and compare against the configured value. A zip code not present in the census data
        // fails the condition (mirrors the Java ZipCodeFilterStrategy).
        var (population, exists) = ZipCodeMetadata.GetMetadata(token);
        if (!exists)
            return false;

        return Compare(clause, ((double)population).CompareTo(clause.Number!.Value));
    }

    private static bool EvaluateConfidence(ConditionClause clause, double confidence)
    {
        var value = clause.Number!.Value;
        return clause.Operator switch
        {
            "==" or "is" => Math.Abs(confidence - value) < 0.0001,
            "!=" or "is not" => Math.Abs(confidence - value) >= 0.0001,
            _ => Compare(clause, confidence.CompareTo(value))
        };
    }

    // token and context: an exact comparison, a case-insensitive prefix, or an ordinal ordering.
    private static bool EvaluateText(ConditionClause clause, string actual)
    {
        return clause.Operator == "startswith"
            ? actual.StartsWith(clause.Text, StringComparison.OrdinalIgnoreCase)
            : Compare(clause, string.Compare(actual, clause.Text, StringComparison.Ordinal));
    }

    private static bool EvaluateType(ConditionClause clause, string? classification)
    {
        var equal = classification != null && classification.Equals(clause.Text, StringComparison.OrdinalIgnoreCase);
        return clause.Operator is "==" or "is" ? equal : !equal;
    }

    // Applies the clause's operator to the result of comparing the actual value with the clause's.
    private static bool Compare(ConditionClause clause, int comparison)
    {
        return clause.Operator switch
        {
            "==" or "is" => comparison == 0,
            "!=" or "is not" => comparison != 0,
            ">" => comparison > 0,
            "<" => comparison < 0,
            ">=" => comparison >= 0,
            "<=" => comparison <= 0,
            _ => throw Unreachable(clause)
        };
    }

    // The parser accepts only the fields and operators handled above, so reaching here is a bug.
    private static InvalidConditionException Unreachable(ConditionClause clause) =>
        new($"has a comparison on '{clause.Field}' with '{clause.Operator}', which cannot be evaluated");
}
