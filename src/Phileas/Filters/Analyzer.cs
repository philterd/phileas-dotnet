/*
 * Copyright 2024 Philterd, LLC
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

using Phileas.Model.Filtering;

namespace Phileas.Filters;

public class Analyzer
{
    public ISet<string>? ContextualTerms { get; }
    public IList<FilterPattern> FilterPatterns { get; }

    public Analyzer(params FilterPattern[] patterns)
    {
        FilterPatterns = patterns.ToList();
    }

    public Analyzer(ISet<string> contextualTerms, params FilterPattern[] patterns)
    {
        ContextualTerms = contextualTerms;
        FilterPatterns = patterns.ToList();
    }
}
