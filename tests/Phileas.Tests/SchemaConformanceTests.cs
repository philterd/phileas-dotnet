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

using System.Reflection;
using System.Text.Json;
using Phileas.Policy.Filters.Strategies;
using Xunit;
using PolicySchema = Phileas.Policy.PolicySchema;

namespace Phileas.Tests;

public class SchemaConformanceTests
{
    [Fact]
    public void EveryStrategyDeclaredBySchemaIsKnownToPhileas()
    {
        var phileasConstants = StringConstantsOf(typeof(AbstractFilterStrategy));
        var schemaStrategies = StrategyNamesIn(PolicySchema.GetSchema());

        Assert.NotEmpty(schemaStrategies);

        foreach (var strategy in schemaStrategies)
        {
            Assert.True(phileasConstants.Contains(strategy),
                $"The policy schema declares strategy '{strategy}' but Phileas has no matching constant "
                + "in AbstractFilterStrategy. The published schema and the runtime have drifted.");
        }
    }

    /// <summary>Every public const (static literal) string field value declared on the type.</summary>
    private static HashSet<string> StringConstantsOf(Type type)
    {
        var values = new HashSet<string>();
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            {
                if (field.GetRawConstantValue() is string s)
                {
                    values.Add(s);
                }
            }
        }

        return values;
    }

    /// <summary>The union of every <c>strategy</c> property enum anywhere in the schema.</summary>
    private static HashSet<string> StrategyNamesIn(string schemaJson)
    {
        var names = new HashSet<string>();
        using var document = JsonDocument.Parse(schemaJson);
        CollectStrategyEnums(document.RootElement, names);
        return names;
    }

    private static void CollectStrategyEnums(JsonElement element, HashSet<string> output)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("strategy")
                    && property.Value.ValueKind == JsonValueKind.Object
                    && property.Value.TryGetProperty("enum", out var enumArray)
                    && enumArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var enumValue in enumArray.EnumerateArray())
                    {
                        if (enumValue.ValueKind == JsonValueKind.String)
                        {
                            output.Add(enumValue.GetString()!);
                        }
                    }
                }

                CollectStrategyEnums(property.Value, output);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var value in element.EnumerateArray())
            {
                CollectStrategyEnums(value, output);
            }
        }
    }
}
