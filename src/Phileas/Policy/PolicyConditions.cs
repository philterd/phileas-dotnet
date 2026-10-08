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

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;
using Phileas.Filters.Conditions;
using Phileas.Policy.Filters.Strategies;

namespace Phileas.Policy;

/// <summary>
///     Checks every strategy condition in a policy, so a condition that does not parse is rejected when the policy
///     is loaded rather than evaluated. See philterd/phileas-dotnet#143.
/// </summary>
public static class PolicyConditions
{
    // Reflection metadata per model type, read once: looking it up on every visit made a check of a policy with
    // every filter configured cost about 0.45 ms, paid on every Filter call.
    private static readonly ConcurrentDictionary<Type, (PropertyInfo Property, string Name)[]> Properties = new();

    /// <summary>
    ///     Returns one entry per strategy whose condition does not parse, as <c>location: reason</c>. The location is
    ///     the strategy's JSON path, followed by its <c>id</c> when it has one. The condition itself is never
    ///     included, since it can hold a value from the data being redacted.
    /// </summary>
    public static IReadOnlyList<string> GetErrors(Policy policy)
    {
        var errors = new List<string>();
        Walk(policy, "", errors);
        return errors;
    }

    /// <summary>Throws <see cref="PolicyValidationException" /> when any strategy condition does not parse.</summary>
    public static void Validate(Policy policy)
    {
        var errors = GetErrors(policy);
        if (errors.Count > 0)
            throw new PolicyValidationException(errors,
                "The policy has strategy conditions that do not parse, so they cannot be applied");
    }

    private static void Walk(object? value, string path, List<string> errors)
    {
        switch (value)
        {
            case null or string:
                return;
            case AbstractFilterStrategy strategy:
                if (!string.IsNullOrWhiteSpace(strategy.Condition)
                    && ConditionParser.GetError(strategy.Condition) is { } reason)
                {
                    var id = string.IsNullOrEmpty(strategy.Id) ? "" : $" (id \"{strategy.Id}\")";
                    errors.Add($"{path}{id}: the condition {reason}");
                }
                return;
            case IDictionary:
                return;
            case IEnumerable items:
                var index = 0;
                foreach (var item in items)
                    Walk(item, $"{path}/{index++}", errors);
                return;
        }

        foreach (var (property, name) in Properties.GetOrAdd(value.GetType(), PropertiesOf))
            Walk(property.GetValue(value), $"{path}/{name}", errors);
    }

    // Only the policy model holds strategies, so a type outside its namespace has nothing to walk. Value-typed
    // and string properties cannot hold a strategy either.
    private static (PropertyInfo, string)[] PropertiesOf(Type type)
    {
        if (type.Namespace == null || !type.Namespace.StartsWith("Phileas.Policy", StringComparison.Ordinal))
            return Array.Empty<(PropertyInfo, string)>();

        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0
                               && !property.PropertyType.IsValueType && property.PropertyType != typeof(string))
            .Select(property => (property,
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name))
            .ToArray();
    }
}
