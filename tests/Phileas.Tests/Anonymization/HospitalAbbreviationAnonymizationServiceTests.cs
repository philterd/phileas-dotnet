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

using Phileas.Services;
using Phileas.Services.Anonymization;
using Xunit;

namespace Phileas.Tests.Anonymization;

public class HospitalAbbreviationAnonymizationServiceTests
{
    private const string Token = "MGH";

    [Fact]
    public void RealisticReplacement()
    {
        var service = new HospitalAbbreviationAnonymizationService(new InMemoryContextService(), new Random(), AnonymizationMethod.Realistic);
        const string token = Token;
        var replacement = service.Anonymize(token);
        Assert.NotNull(replacement);
        Assert.NotEmpty(replacement);
        Assert.NotEqual(token, replacement);
    }

    [Fact]
    public void UuidReplacement()
    {
        var service = new HospitalAbbreviationAnonymizationService(new InMemoryContextService(), new Random(), AnonymizationMethod.Uuid);
        var replacement = service.Anonymize(Token);
        Assert.NotEqual(Token, replacement);
        Assert.True(replacement.Length >= 32);
    }

    [Fact]
    public void FromListReplacement()
    {
        var candidates = new List<string> { "AAA", "BBB", "CCC" };
        var service = new HospitalAbbreviationAnonymizationService(new InMemoryContextService(), new Random(), candidates);
        Assert.Contains(service.Anonymize(Token), candidates);
    }

    [Fact]
    public void ExposesContextService()
    {
        var service = new HospitalAbbreviationAnonymizationService(new InMemoryContextService());
        Assert.NotNull(service.GetContextService());
    }
}
