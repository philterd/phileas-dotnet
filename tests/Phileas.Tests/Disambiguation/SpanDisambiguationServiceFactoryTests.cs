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

using Phileas.Model;
using Phileas.Services.Disambiguation;
using Phileas.Services.Disambiguation.Vector;
using Xunit;

namespace Phileas.Tests.Disambiguation;

public class SpanDisambiguationServiceFactoryTests
{
    private static ISpanDisambiguationService Build(bool enabled, IVectorService vectorService)
    {
        return SpanDisambiguationServiceFactory.Create(
            new SpanDisambiguationOptions { Enabled = enabled }, vectorService);
    }

    [Fact]
    public void EnabledConfigurationYieldsTheVectorBasedService()
    {
        Assert.IsType<VectorBasedSpanDisambiguationService>(Build(true, new InMemoryVectorService()));
    }

    [Fact]
    public void DisabledConfigurationYieldsTheNoOpService()
    {
        Assert.IsType<NoOpSpanDisambiguationService>(Build(false, new InMemoryVectorService()));
    }

    [Fact]
    public void NoOpServiceLeavesSpansUnchangedAndDoesNotTrain()
    {
        var vectorService = new InMemoryVectorService();
        var service = Build(false, vectorService);

        var asSsn = Span.Make(0, 4, FilterType.Ssn, "c", 0.5, "123456789", "x", "", false, true,
            new[] { "phone", "number" }, 0);
        var asPhone = Span.Make(0, 4, FilterType.PhoneNumber, "c", 0.5, "123456789", "x", "", false, true,
            new[] { "phone", "number" }, 0);

        var spans = new List<Span> { asSsn, asPhone };
        var result = service.Disambiguate("c", spans);

        Assert.Equal(spans, result);
        Assert.True(vectorService.GetVectorRepresentation("c", FilterType.PhoneNumber).IsEmpty);

        // The three-argument form keeps the first candidate.
        Assert.Equal(FilterType.Ssn,
            service.Disambiguate("c", new List<FilterType> { FilterType.Ssn, FilterType.PhoneNumber }, asSsn));
    }
}
