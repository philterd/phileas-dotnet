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

using System.Collections.Concurrent;
using System.Globalization;

namespace Phileas.Filters.Conditions;

/// <summary>One <c>field op value</c> comparison in a strategy condition.</summary>
/// <param name="Field">The field, lowercased: population, token, type, confidence or context.</param>
/// <param name="Operator">The operator, lowercased, with <c>is not</c> as one operator.</param>
/// <param name="Text">The value of a quoted string, without the quotes, or the digits of a number.</param>
/// <param name="Number">The value as a number when it was written as one; otherwise <see langword="null" />.</param>
public sealed record ConditionClause(string Field, string Operator, string Text, double? Number);

/// <summary>Thrown for a strategy condition that does not parse. The message never contains the condition.</summary>
public class InvalidConditionException : Exception
{
    /// <summary>Creates the exception with a reason that does not quote the condition.</summary>
    public InvalidConditionException(string reason) : base(reason)
    {
    }
}

/// <summary>
///     Parses strategy conditions: one or more <c>field op value</c> comparisons joined with <c>and</c>, the grammar of
///     the Java port's <c>FilterCondition.g4</c>. A condition that does not fit it is rejected with a reason, never
///     treated as satisfied. See philterd/phileas-dotnet#143.
/// </summary>
public static class ConditionParser
{
    private static readonly HashSet<string> Fields = new() { "population", "token", "type", "confidence", "context" };

    private static readonly HashSet<string> SymbolOperators = new() { "==", "!=", ">", "<", ">=", "<=" };

    private static readonly HashSet<string> EqualityOperators = new() { "==", "!=", "is", "is not" };

    // Parsed once per distinct condition; a policy has few, and evaluation runs once per span. Bounded, because a
    // long-running service accepting policies over its API would otherwise keep every condition it ever saw.
    private const int CacheLimit = 4096;

    private static readonly ConcurrentDictionary<string, (IReadOnlyList<ConditionClause>? Clauses, string? Error)>
        Cache = new();

    /// <summary>Parses <paramref name="condition" />, or throws <see cref="InvalidConditionException" />.</summary>
    public static IReadOnlyList<ConditionClause> Parse(string condition)
    {
        var (clauses, error) = Lookup(condition);
        return clauses ?? throw new InvalidConditionException(error!);
    }

    /// <summary>
    ///     Returns why <paramref name="condition" /> does not parse, or <see langword="null" /> when it does. The
    ///     reason names the grammar's own terms and never quotes the condition, which can hold a value from the
    ///     data being redacted.
    /// </summary>
    public static string? GetError(string condition) => Lookup(condition).Error;

    private static (IReadOnlyList<ConditionClause>? Clauses, string? Error) Lookup(string condition)
    {
        if (Cache.TryGetValue(condition, out var parsed)) return parsed;
        if (Cache.Count >= CacheLimit) Cache.Clear();
        return Cache.GetOrAdd(condition, ParseUncached);
    }

    private static (IReadOnlyList<ConditionClause>?, string?) ParseUncached(string condition)
    {
        try
        {
            return (ParseClauses(Tokenize(condition)), null);
        }
        catch (InvalidConditionException e)
        {
            return (null, e.Message);
        }
    }

    private enum Kind
    {
        Word,
        Symbol,
        String,
        Number
    }

    private readonly record struct Token(Kind Kind, string Text);

    private static List<Token> Tokenize(string condition)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < condition.Length)
        {
            var c = condition[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '"')
            {
                var close = condition.IndexOf('"', i + 1);
                if (close < 0) throw new InvalidConditionException("has a quoted value with no closing quote");
                tokens.Add(new Token(Kind.String, condition[(i + 1)..close]));
                i = close + 1;
            }
            else if (char.IsAsciiDigit(c))
            {
                var start = i;
                while (i < condition.Length && char.IsAsciiDigit(condition[i])) i++;
                if (i + 1 < condition.Length && condition[i] == '.' && char.IsAsciiDigit(condition[i + 1]))
                {
                    i++;
                    while (i < condition.Length && char.IsAsciiDigit(condition[i])) i++;
                }
                tokens.Add(new Token(Kind.Number, condition[start..i]));
            }
            else if (char.IsAsciiLetter(c) || c == '_')
            {
                var start = i;
                while (i < condition.Length && (char.IsAsciiLetterOrDigit(condition[i]) || condition[i] == '_')) i++;
                tokens.Add(new Token(Kind.Word, condition[start..i].ToLowerInvariant()));
            }
            else
            {
                var two = i + 1 < condition.Length ? condition.Substring(i, 2) : null;
                if (two is "==" or "!=" or ">=" or "<=" or "&&" or "||")
                {
                    tokens.Add(new Token(Kind.Symbol, two));
                    i += 2;
                }
                else if (c is '>' or '<' or '=' or '(' or ')' or '!')
                {
                    tokens.Add(new Token(Kind.Symbol, c.ToString()));
                    i++;
                }
                else
                {
                    throw new InvalidConditionException("contains a character the condition grammar does not use");
                }
            }
        }

        return tokens;
    }

    private static List<ConditionClause> ParseClauses(List<Token> tokens)
    {
        if (tokens.Count == 0) throw new InvalidConditionException("is empty");

        var clauses = new List<ConditionClause>();
        var i = 0;
        while (true)
        {
            clauses.Add(ParseClause(tokens, ref i));
            if (i == tokens.Count) return clauses;

            var next = tokens[i];
            if (next is { Kind: Kind.Word, Text: "and" })
            {
                i++;
                if (i == tokens.Count) throw new InvalidConditionException("ends with 'and'");
                continue;
            }

            throw new InvalidConditionException(next switch
            {
                { Kind: Kind.Word, Text: "or" } or { Kind: Kind.Symbol, Text: "||" } =>
                    "joins comparisons with 'or', which is not supported; only 'and' is",
                { Kind: Kind.Symbol, Text: "&&" } => "joins comparisons with '&&'; use 'and'",
                { Kind: Kind.Symbol, Text: "(" or ")" } => "uses parentheses, which are not supported",
                _ => "continues after a complete comparison with something other than 'and'"
            });
        }
    }

    private static ConditionClause ParseClause(List<Token> tokens, ref int i)
    {
        var fieldToken = tokens[i];
        if (fieldToken.Text is "(" or ")")
            throw new InvalidConditionException("uses parentheses, which are not supported");
        if (fieldToken.Kind != Kind.Word || !Fields.Contains(fieldToken.Text))
            throw new InvalidConditionException(
                "has a comparison that does not start with a field: token, context, confidence, population or type");
        var field = fieldToken.Text;
        i++;

        if (i == tokens.Count) throw new InvalidConditionException($"has no operator after '{field}'");
        var op = ParseOperator(tokens, ref i, field);

        if (i == tokens.Count) throw new InvalidConditionException($"has no value after '{field} {op}'");
        var value = tokens[i];
        i++;

        if (value.Kind == Kind.Word)
            throw new InvalidConditionException(field == "token" && value.Text is "birthdate" or "deathdate"
                ? $"uses 'token {op} {value.Text}', which is not supported yet"
                : $"compares '{field}' with a value that is neither a quoted string nor a number");
        if (value.Kind == Kind.Symbol)
            throw new InvalidConditionException($"has no value after '{field} {op}'");

        double? number = value.Kind == Kind.Number
            ? double.Parse(value.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture)
            : null;

        switch (field)
        {
            case "confidence" or "population":
                if (number == null)
                    throw new InvalidConditionException($"compares '{field}' with a value that is not a number");
                if (op == "startswith")
                    throw new InvalidConditionException($"uses 'startswith' on '{field}', which is a number");
                break;
            case "type":
                if (!EqualityOperators.Contains(op))
                    throw new InvalidConditionException(
                        $"uses '{op}' on 'type', which supports only ==, !=, is and is not");
                break;
        }

        return new ConditionClause(field, op, value.Text, number);
    }

    private static string ParseOperator(List<Token> tokens, ref int i, string field)
    {
        var token = tokens[i];
        i++;

        if (token.Kind == Kind.Symbol && SymbolOperators.Contains(token.Text)) return token.Text;
        if (token is { Kind: Kind.Word, Text: "startswith" }) return "startswith";
        if (token is { Kind: Kind.Word, Text: "is" })
        {
            if (i < tokens.Count && tokens[i] is { Kind: Kind.Word, Text: "not" })
            {
                i++;
                return "is not";
            }

            return "is";
        }

        throw new InvalidConditionException(token is { Kind: Kind.Symbol, Text: "=" }
            ? $"uses '=' after '{field}'; the equality operator is '=='"
            : $"has no operator after '{field}': ==, !=, >, <, >=, <=, startswith, is or is not");
    }
}
