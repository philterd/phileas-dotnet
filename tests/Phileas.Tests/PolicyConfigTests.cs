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

using Phileas.Policy;
using Xunit;
using PhileasPolicy = Phileas.Policy.Policy;
using PolicySchema = Phileas.Policy.PolicySchema;

namespace Phileas.Tests;

public class PolicyConfigTests
{
    [Fact]
    public void Config_HasCanonicalDefaults()
    {
        var config = new Config();

        Assert.False(config.Splitting.Enabled);
        Assert.Equal(10000, config.Splitting.Threshold);
        Assert.Equal("newline", config.Splitting.Method);
        Assert.True(config.Analysis.Identification);
        Assert.Equal("black", config.Pdf.RedactionColor);
        Assert.Equal(150, config.Pdf.Dpi);
        Assert.True(config.PostFilters.RemoveTrailingPeriods);
        Assert.True(config.PostFilters.RemoveTrailingSpaces);
        Assert.True(config.PostFilters.RemoveTrailingNewLines);
    }

    [Fact]
    public void FromPhiSQL_CompilesConfigureBlocks()
    {
        // The CONFIGURE statements compile into the canonical config sub-objects and deserialize into
        // the .NET Config model, proving the model matches the schema phisql emits.
        const string phisql = """
                              POLICY p;
                              REDACT SSN WITH REDACT;
                              CONFIGURE SPLITTING ( enabled = true, threshold = 5000 );
                              CONFIGURE POSTFILTERS ( removeTrailingPeriods = false );
                              CONFIGURE PDF ( dpi = 200 );
                              CONFIGURE ANALYSIS ( identification = false );
                              """;

        var policy = PhileasPolicy.FromPhiSQL(phisql);

        Assert.True(policy.Config.Splitting.Enabled);
        Assert.Equal(5000, policy.Config.Splitting.Threshold);
        Assert.False(policy.Config.PostFilters.RemoveTrailingPeriods);
        Assert.Equal(200, policy.Config.Pdf.Dpi);
        Assert.False(policy.Config.Analysis.Identification);
        Assert.NotNull(policy.Identifiers.Ssn);
    }

    [Fact]
    public void SerializedPolicy_HasNoTopLevelNameOrPostFilters()
    {
        // The canonical policy schema is additionalProperties:false at the top level with no "name" and
        // no top-level "postFilters" (postFilters lives under config). A serialized policy must not emit
        // those, or it would fail schema validation.
        var policy = new PhileasPolicy { Name = "ignored-label" };

        var json = PolicySerializer.SerializeToJson(policy);

        Assert.DoesNotContain("\"name\"", json);
        Assert.Contains("\"config\"", json);
        Assert.Contains("\"postFilters\"", json); // present, but nested under config
        Assert.True(PolicySchema.Validate(json), $"Serialized default policy should be schema-valid: {json}");
    }
}
