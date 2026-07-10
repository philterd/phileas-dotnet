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

using Phileas.Filters;
using Phileas.Filters.Strategies.Rules;
using Xunit;

namespace Phileas.Tests;

public class AbbreviateFilterStrategyTests
{
    [Fact]
    public void SurnameFilterStrategy_Abbreviate_ReturnsInitialsForMultipleWords()
    {
        var strategy = new SurnameFilterStrategy { Strategy = AbstractFilterStrategy.Abbreviate };

        var replacement = strategy.GetReplacement("ctx", "John Smith", [], 0.9, null, null, null, null);

        Assert.Equal("JS", replacement.Value);
    }

    [Fact]
    public void FirstNameFilterStrategy_Abbreviate_ReturnsInitialForSingleWord()
    {
        var strategy = new FirstNameFilterStrategy { Strategy = AbstractFilterStrategy.Abbreviate };

        var replacement = strategy.GetReplacement("ctx", "john", [], 0.9, null, null, null, null);

        Assert.Equal("J", replacement.Value);
    }
}
