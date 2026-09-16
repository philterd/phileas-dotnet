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

using System.Text.Json;
using Phileas.Model;
using Phileas.Policy;
using Phileas.Policy.Filters;
using Xunit;

namespace Phileas.Tests;

/// <summary>
///     <c>identifiers.person</c> is the schema's deprecated alias for a single <c>pheyes</c> entry, and
///     Java binds it. This port did not, so a legacy policy using it loaded and detected nothing.
///     See philterd/phileas-dotnet#87.
/// </summary>
public class DeprecatedPersonKeyTests
{
    private const string Body = "{\"phEyeConfiguration\":{\"endpoint\":\"http://pheye:8080\"},"
                                + "\"removePunctuation\":true,\"windowSize\":7,\"priority\":3}";

    private const string PersonPolicy = "{\"identifiers\":{\"person\":" + Body + "}}";
    private const string PhEyesPolicy = "{\"identifiers\":{\"pheyes\":[" + Body + "]}}";

    private static Identifiers Load(string json)
    {
        return PolicySerializer.DeserializeFromJson(json).Identifiers;
    }

    [Fact]
    public void ThePersonKeyBindsAsAPhEyeEntry()
    {
        var entry = Assert.Single(Load(PersonPolicy).PhEyes!);

        Assert.Equal("http://pheye:8080", entry.PhEyeConfiguration!.Endpoint);
        Assert.True(entry.RemovePunctuation);
        Assert.Equal(7, entry.WindowSize);
        Assert.Equal(3, entry.Priority);
    }

    [Fact]
    public void APolicyUsingPersonActivatesThePhEyeFilter()
    {
        // Without the binding the key was skipped, so the policy named a filter and ran none.
        Assert.True(Load(PersonPolicy).HasFilter(FilterType.PhEye));
    }

    [Fact]
    public void PersonAndPhEyesProduceTheSameCanonicalPolicy()
    {
        // The strongest equivalence available without a live PhEye service: if the two policies
        // serialize to the same canonical JSON, the filters built from them are the same, so their
        // detections are too.
        Assert.Equal(
            PolicySerializer.SerializeToJson(PolicySerializer.DeserializeFromJson(PhEyesPolicy)),
            PolicySerializer.SerializeToJson(PolicySerializer.DeserializeFromJson(PersonPolicy)));
    }

    [Fact]
    public void EveryPropertyOfTheEntryIsCarriedAcross()
    {
        // Drift-proof: a property added to PhEye must survive the fold without anyone remembering.
        // Compared through serialization so a nested object such as phEyeConfiguration is compared by
        // its contents; reference equality would pass two different objects and assert nothing.
        const string nested = "{\"phEyeConfiguration\":{\"endpoint\":\"http://pheye:8080\","
                              + "\"labels\":[\"Person\",\"Org\"]},\"thresholds\":{\"PERSON\":0.8},"
                              + "\"removePunctuation\":true,\"windowSize\":7,\"priority\":3}";
        var fromPerson = Assert.Single(Load("{\"identifiers\":{\"person\":" + nested + "}}").PhEyes!);
        var fromPhEyes = Assert.Single(Load("{\"identifiers\":{\"pheyes\":[" + nested + "]}}").PhEyes!);

        var compared = 0;
        foreach (var property in typeof(PhEye).GetProperties())
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0) continue;

            Assert.Equal(
                JsonSerializer.Serialize(property.GetValue(fromPhEyes)),
                JsonSerializer.Serialize(property.GetValue(fromPerson)));
            compared++;
        }

        Assert.True(compared > 5, $"only {compared} properties were compared");
    }

    [Fact]
    public void BothKeysInOnePolicyAreKept()
    {
        const string json = "{\"identifiers\":{\"pheyes\":[{\"phEyeConfiguration\":{\"endpoint\":\"http://a\"}}],"
                            + "\"person\":{\"phEyeConfiguration\":{\"endpoint\":\"http://b\"}}}}";
        var entries = Load(json).PhEyes!;

        Assert.Equal(2, entries.Count);
        Assert.Equal("http://a", entries[0].PhEyeConfiguration!.Endpoint);
        Assert.Equal("http://b", entries[1].PhEyeConfiguration!.Endpoint);
    }

    [Fact]
    public void TheDeprecatedKeyIsNeverWrittenBack()
    {
        var round = PolicySerializer.SerializeToJson(PolicySerializer.DeserializeFromJson(PersonPolicy));

        Assert.DoesNotContain("\"person\"", round);
        Assert.Contains("\"pheyes\"", round);
    }

    [Fact]
    public void ADisabledPersonEntryIsNotActive()
    {
        // enabled:false has to reach the folded entry, so the two features compose. See #123.
        const string json = "{\"identifiers\":{\"person\":{\"enabled\":false,"
                            + "\"phEyeConfiguration\":{\"endpoint\":\"http://x\"}}}}";

        Assert.False(Load(json).HasFilter(FilterType.PhEye));
    }

    [Fact]
    public void APersonThatIsNotAnObjectIsReported()
    {
        // The schema types person as an object, so a null is a policy error rather than something the
        // fold has to survive.
        Assert.Throws<PolicyValidationException>(
            () => PolicySerializer.DeserializeFromJson("{\"identifiers\":{\"person\":null,\"ssn\":{}}}"));
    }

    [Fact]
    public void APolicyWithNeitherKeyIsUnchanged()
    {
        Assert.Null(Load("{\"identifiers\":{\"ssn\":{}}}").PhEyes);
        Assert.False(Load("{\"identifiers\":{\"ssn\":{}}}").HasFilter(FilterType.PhEye));
    }

    [Fact]
    public void ThePolicyValidatesAgainstTheSchemaEitherWay()
    {
        // The schema declares person as deprecated but valid, so a legacy policy still loads.
        Assert.True(PolicySchema.Validate(PersonPolicy),
            string.Join("; ", PolicySchema.GetValidationErrors(PersonPolicy)));
        Assert.True(PolicySchema.Validate(
            PolicySerializer.SerializeToJson(PolicySerializer.DeserializeFromJson(PersonPolicy))));
    }
}
