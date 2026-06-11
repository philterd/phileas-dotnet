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
using Phileas.Policy;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;

namespace Phileas.Tests;

public class PolicyFromPhiSqlTests
{
    [Fact]
    public void CompilesPhiSqlIntoPolicy()
    {
        const string phisql = """
                              POLICY test_policy DESCRIPTION 'demo';
                              REDACT EMAIL_ADDRESS WITH REDACT;
                              """;

        var policy = PhileasPolicy.FromPhiSQL(phisql);

        // The compiled policy enables the EMAIL_ADDRESS identifier.
        Assert.NotNull(policy.Identifiers.EmailAddress);
    }

    [Fact]
    public void CompiledPolicyEquivalentToJson()
    {
        // A PhiSQL policy and a hand-written JSON policy that target the same identifier deserialize to
        // equal Policy objects, demonstrating PhiSQL is purely an authoring format.
        var fromPhiSQL = PhileasPolicy.FromPhiSQL("REDACT ZIP_CODE WITH REDACT;");

        const string json = "{\"identifiers\": {\"zipCode\":"
                            + " {\"zipCodeFilterStrategy\": [{\"strategy\": \"REDACT\"}]}}}";
        var fromJson = PolicySerializer.DeserializeFromJson(json);

        Assert.NotNull(fromPhiSQL.Identifiers.ZipCode);

        // Compare canonical JSON forms. Normalize any random "id" field out before comparing.
        var normalizedFromJson = NormalizeIds(PolicySerializer.SerializeToJson(fromJson));
        var normalizedFromPhiSQL = NormalizeIds(PolicySerializer.SerializeToJson(fromPhiSQL));
        Assert.Equal(normalizedFromJson, normalizedFromPhiSQL);
    }

    [Fact]
    public void ThrowsOnSyntaxError()
    {
        // "REDACTT" is not a valid keyword, so parsing fails.
        var ex = Assert.Throws<PolicyCompilationException>(
            () => PhileasPolicy.FromPhiSQL("REDACTT EMAIL_ADDRESS WITH REDACT;"));

        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void ThrowsOnSemanticError()
    {
        // Syntactically valid, but NOT_A_REAL_ENTITY is not a known entity type, so compilation fails.
        var ex = Assert.Throws<PolicyCompilationException>(
            () => PhileasPolicy.FromPhiSQL("REDACT NOT_A_REAL_ENTITY WITH REDACT;"));

        Assert.NotNull(ex.InnerException);
    }

    private static string NormalizeIds(string json)
    {
        return Regex.Replace(json, "\"id\":\"[^\"]*\"", "\"id\":\"\"");
    }
}
