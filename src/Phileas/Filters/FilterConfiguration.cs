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

using Phileas.Policy;

namespace Phileas.Filters;

public class FilterConfiguration
{
    public IList<AbstractFilterStrategy>? Strategies { get; private set; }
    public ISet<string>? Ignored { get; private set; }
    public ISet<string>? IgnoredFiles { get; private set; }
    public IList<IgnoredPattern>? IgnoredPatterns { get; private set; }
    public Crypto? Crypto { get; private set; }
    public Fpe? Fpe { get; private set; }
    public int WindowSize { get; private set; } = 5;
    public int Priority { get; private set; } = 0;

    private FilterConfiguration() { }

    public class Builder
    {
        private readonly FilterConfiguration _config = new FilterConfiguration();

        public Builder WithStrategies(IList<AbstractFilterStrategy> strategies) { _config.Strategies = strategies; return this; }
        public Builder WithIgnored(ISet<string> ignored) { _config.Ignored = ignored; return this; }
        public Builder WithIgnoredFiles(ISet<string> ignoredFiles) { _config.IgnoredFiles = ignoredFiles; return this; }
        public Builder WithIgnoredPatterns(IList<IgnoredPattern> ignoredPatterns) { _config.IgnoredPatterns = ignoredPatterns; return this; }
        public Builder WithCrypto(Crypto crypto) { _config.Crypto = crypto; return this; }
        public Builder WithFpe(Fpe fpe) { _config.Fpe = fpe; return this; }
        public Builder WithWindowSize(int windowSize) { _config.WindowSize = windowSize; return this; }
        public Builder WithPriority(int priority) { _config.Priority = priority; return this; }
        public FilterConfiguration Build() => _config;
    }
}
