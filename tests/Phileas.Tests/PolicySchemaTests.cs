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

using Xunit;
using PhileasPolicySchema = Phileas.Policy.PolicySchema;
using PhiSqlPolicySchema = Philterd.PhiSql.PolicySchema;

namespace Phileas.Tests;

public class PolicySchemaTests
{
    [Fact]
    public void SchemaIsAvailableFromPhisql()
    {
        var schema = PhiSqlPolicySchema.GetSchema();

        Assert.NotNull(schema);
        Assert.False(string.IsNullOrWhiteSpace(schema));
    }

    [Fact]
    public void SupportedSchemaVersionIsAvailableFromPhisql()
    {
        Assert.Equal("1.3.0", PhiSqlPolicySchema.GetSupportedSchemaVersion());
    }

    [Fact]
    public void SupportedSchemaVersionIsAvailable()
    {
        Assert.Equal("1.3.0", PhileasPolicySchema.GetSupportedSchemaVersion());
    }

    [Fact]
    public void SchemaIsAvailable()
    {
        var schema = PhileasPolicySchema.GetSchema();

        Assert.NotNull(schema);
        Assert.False(string.IsNullOrWhiteSpace(schema));
    }
}
