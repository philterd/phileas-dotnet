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
using PolicySchema = Phileas.Policy.PolicySchema;

namespace Phileas.Tests;

public class PolicySchemaValidationTests
{
    [Fact]
    public void ValidateValidPolicy()
    {
        // A simple valid policy.
        const string jsonPolicy = "{\"identifiers\": {\"zipCode\": {\"enabled\": true}}}";

        var valid = PolicySchema.Validate(jsonPolicy);

        Assert.True(valid);
    }

    [Fact]
    public void ValidateInvalidPolicy()
    {
        // zipCode should be an object, not a string.
        const string jsonPolicy = "{\"identifiers\": {\"zipCode\": \"should-be-an-object\"}}";

        var valid = PolicySchema.Validate(jsonPolicy);

        Assert.False(valid);
    }

    [Fact]
    public void ValidateMalformedJson()
    {
        const string jsonPolicy = "{\"identifiers\": ";

        var valid = PolicySchema.Validate(jsonPolicy);

        Assert.False(valid);
    }

    [Fact]
    public void ValidateEmptyPolicy()
    {
        // {} is a valid (empty) policy.
        Assert.True(PolicySchema.Validate("{}"));
    }

    [Fact]
    public void ValidateEinPolicy()
    {
        // The ein identifier and its onlyValidPrefixes option are part of schema 1.2.0.
        const string jsonPolicy =
            "{\"identifiers\": {\"ein\": {\"onlyValidPrefixes\": true, " +
            "\"einFilterStrategies\": [{\"strategy\": \"REDACT\"}]}}}";

        Assert.True(PolicySchema.Validate(jsonPolicy));
    }

    [Fact]
    public void ValidateMapReplacePolicy()
    {
        // MAP_REPLACE strategy fields and the top-level generators block are part of schema 1.2.0.
        const string jsonPolicy =
            "{\"generators\": {\"local\": {\"type\": \"ollama\", \"endpoint\": \"http://localhost:11434\", " +
            "\"model\": \"llama3.1\", \"prompt\": \"Rewrite {{token}}.\", \"timeoutMs\": 2000}}, " +
            "\"identifiers\": {\"ssn\": {\"ssnFilterStrategies\": [{\"strategy\": \"MAP_REPLACE\", " +
            "\"mappings\": {\"123-45-6789\": \"000-00-0000\"}, \"caseSensitive\": false, " +
            "\"generator\": \"local\", \"fallbackStrategy\": \"REDACT\"}]}}}";

        Assert.True(PolicySchema.Validate(jsonPolicy));
    }
}
