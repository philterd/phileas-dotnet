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

using Phileas.Filters.Rules.Dictionary;
using Phileas.Model;
using Xunit;
using static Phileas.Tests.Dictionaries.DictionaryTestSupport;

namespace Phileas.Tests.Dictionaries;

public class HospitalFilterTests
{
    [Fact]
    public void Filter1()
    {
        var filter = new FuzzyDictionaryFilter(FilterType.Hospital, Config(), SensitivityLevel.Low, true);
        var filtered = filter.Filter(GetPolicy(), "context", Piece, "UCLA Medical Center");
        Assert.Single(filtered.Spans);
        Assert.Equal("UCLA Medical Center", filtered.Spans[0].Text);
    }
}
