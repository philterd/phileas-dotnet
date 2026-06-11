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
using Phileas.Model.Metadata;

namespace Phileas.Filters.Conditions;

/// <summary>
///     Evaluates filter strategy conditions based on the grammar defined in FilterCondition.g4
///     Supported conditions:
///     - population COMPARATOR NUMBER
///     - token COMPARATOR WORD
///     - type COMPARATOR TYPE
///     - confidence COMPARATOR NUMBER
///     - context COMPARATOR WORD
///     - Multiple conditions combined with AND
/// </summary>
public static class ConditionEvaluator
{
    private static readonly ZipCodeMetadataService ZipCodeMetadata = new();

    private static readonly Regex ConditionPattern = new(
        @"^\s*(?<field>population|token|type|confidence|context)\s+(?<op>>|<|<=|>=|==|!=|startswith|is|is not)\s+(?<value>""[^""]*""|\d+(?:\.\d+)?)\s*(?<and>and\s+(?<rest>.+))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

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
    public static bool Evaluate(
        string condition,
        string context,
        string token,
        double confidence,
        string? classification)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return true;

        return EvaluateSingle(condition.Trim(), context, token, confidence, classification);
    }

    private static bool EvaluateSingle(
        string condition,
        string context,
        string token,
        double confidence,
        string? classification)
    {
        var match = ConditionPattern.Match(condition);
        if (!match.Success)
            return true; // Invalid condition always evaluates to true

        var field = match.Groups["field"].Value.ToLowerInvariant();
        var op = match.Groups["op"].Value.ToLowerInvariant();
        var valueStr = match.Groups["value"].Value;
        var hasAnd = match.Groups["and"].Success;
        var rest = hasAnd ? match.Groups["rest"].Value : null;

        // Evaluate current condition
        var result = field switch
        {
            "population" => EvaluatePopulation(op, valueStr, token),
            "token" => EvaluateToken(op, valueStr, token),
            "type" => EvaluateType(op, valueStr, classification),
            "confidence" => EvaluateConfidence(op, valueStr, confidence),
            "context" => EvaluateContext(op, valueStr, context),
            _ => true
        };

        // If there's an AND clause, evaluate it recursively
        if (hasAnd && !string.IsNullOrWhiteSpace(rest))
            return result && EvaluateSingle(rest, context, token, confidence, classification);

        return result;
    }

    private static bool EvaluatePopulation(string op, string valueStr, string token)
    {
        // The population condition applies to zip-code tokens: look up the census population for the
        // token and compare against the configured value. A zip code not present in the census data
        // fails the condition (mirrors the Java ZipCodeFilterStrategy).
        if (!int.TryParse(valueStr, out var value))
            return false;

        var (population, exists) = ZipCodeMetadata.GetMetadata(token);
        if (!exists)
            return false;

        return op switch
        {
            ">" => population > value,
            "<" => population < value,
            ">=" => population >= value,
            "<=" => population <= value,
            "==" or "is" => population == value,
            "!=" or "is not" => population != value,
            _ => false
        };
    }

    private static bool EvaluateToken(string op, string valueStr, string token)
    {
        var value = UnquoteString(valueStr);

        return op switch
        {
            "==" or "is" => token == value,
            "!=" or "is not" => token != value,
            "startswith" => token.StartsWith(value, StringComparison.OrdinalIgnoreCase),
            ">" => string.Compare(token, value, StringComparison.Ordinal) > 0,
            "<" => string.Compare(token, value, StringComparison.Ordinal) < 0,
            ">=" => string.Compare(token, value, StringComparison.Ordinal) >= 0,
            "<=" => string.Compare(token, value, StringComparison.Ordinal) <= 0,
            _ => true
        };
    }

    private static bool EvaluateType(string op, string valueStr, string? classification)
    {
        if (classification == null)
            return op is "!=" or "is not";

        var value = UnquoteString(valueStr);

        return op switch
        {
            "==" or "is" => classification.Equals(value, StringComparison.OrdinalIgnoreCase),
            "!=" or "is not" => !classification.Equals(value, StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private static bool EvaluateConfidence(string op, string valueStr, double confidence)
    {
        if (!double.TryParse(valueStr, out var value))
            return true;

        return op switch
        {
            "==" or "is" => Math.Abs(confidence - value) < 0.0001,
            "!=" or "is not" => Math.Abs(confidence - value) >= 0.0001,
            ">" => confidence > value,
            "<" => confidence < value,
            ">=" => confidence >= value,
            "<=" => confidence <= value,
            _ => true
        };
    }

    private static bool EvaluateContext(string op, string valueStr, string context)
    {
        var value = UnquoteString(valueStr);

        return op switch
        {
            "==" or "is" => context == value,
            "!=" or "is not" => context != value,
            "startswith" => context.StartsWith(value, StringComparison.OrdinalIgnoreCase),
            ">" => string.Compare(context, value, StringComparison.Ordinal) > 0,
            "<" => string.Compare(context, value, StringComparison.Ordinal) < 0,
            ">=" => string.Compare(context, value, StringComparison.Ordinal) >= 0,
            "<=" => string.Compare(context, value, StringComparison.Ordinal) <= 0,
            _ => true
        };
    }

    private static string UnquoteString(string quoted)
    {
        if (quoted.StartsWith('"') && quoted.EndsWith('"') && quoted.Length >= 2)
            return quoted[1..^1];
        return quoted;
    }
}