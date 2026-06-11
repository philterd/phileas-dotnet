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

namespace Phileas.Filters;

/// <summary>
///     Immutable runtime configuration for a filter instance. Use the nested <see cref="Builder" />
///     class to construct instances.
/// </summary>
public class FilterConfiguration
{
    private FilterConfiguration()
    {
    }

    /// <summary>Gets the list of replacement strategies to apply in order.</summary>
    public IList<AbstractFilterStrategy>? Strategies { get; private set; }

    /// <summary>Gets the set of exact token values that should not be filtered.</summary>
    public ISet<string>? Ignored { get; private set; }

    /// <summary>Gets the set of file paths whose contents list additional ignored values.</summary>
    public ISet<string>? IgnoredFiles { get; private set; }

    /// <summary>Gets the list of regex-based patterns for tokens that should not be filtered.</summary>
    public IList<IgnoredPattern>? IgnoredPatterns { get; private set; }

    /// <summary>Gets the AES crypto settings used by the <c>CRYPTO_REPLACE</c> strategy.</summary>
    public Crypto? Crypto { get; private set; }

    /// <summary>Gets the format-preserving encryption settings used by the <c>FPE_ENCRYPT_REPLACE</c> strategy.</summary>
    public Fpe? Fpe { get; private set; }

    /// <summary>Gets the number of words on each side of a matched entity to include in the context window. Defaults to 5.</summary>
    public int WindowSize { get; private set; } = 5;

    /// <summary>Gets the filter priority used when resolving overlapping spans. Defaults to 0.</summary>
    public int Priority { get; private set; }

    /// <summary>Gets the post-filter settings that trim trailing characters from detected entity text.</summary>
    public Policy.PostFilters? PostFilters { get; private set; }

    /// <summary>
    ///     Gets the per-pattern regex match budget in milliseconds. A pattern that exceeds the budget is
    ///     aborted and yields no spans, so a catastrophic user-supplied pattern cannot stall filtering.
    ///     Defaults to 1000.
    /// </summary>
    public long RegexTimeoutMs { get; private set; } = 1000;

    /// <summary>
    ///     Fluent builder for <see cref="FilterConfiguration" />.
    /// </summary>
    public class Builder
    {
        private readonly FilterConfiguration _config = new();

        /// <summary>Sets the replacement strategies.</summary>
        public Builder WithStrategies(IList<AbstractFilterStrategy> strategies)
        {
            _config.Strategies = strategies;
            return this;
        }

        /// <summary>Sets the ignored exact-match tokens.</summary>
        public Builder WithIgnored(ISet<string> ignored)
        {
            _config.Ignored = ignored;
            return this;
        }

        /// <summary>Sets the ignored-files paths.</summary>
        public Builder WithIgnoredFiles(ISet<string> ignoredFiles)
        {
            _config.IgnoredFiles = ignoredFiles;
            return this;
        }

        /// <summary>Sets the regex-based ignored patterns.</summary>
        public Builder WithIgnoredPatterns(IList<IgnoredPattern> ignoredPatterns)
        {
            _config.IgnoredPatterns = ignoredPatterns;
            return this;
        }

        /// <summary>Sets the AES crypto configuration.</summary>
        public Builder WithCrypto(Crypto? crypto)
        {
            _config.Crypto = crypto;
            return this;
        }

        /// <summary>Sets the format-preserving encryption configuration.</summary>
        public Builder WithFpe(Fpe? fpe)
        {
            _config.Fpe = fpe;
            return this;
        }

        /// <summary>Sets the context-window size (number of words on each side).</summary>
        public Builder WithWindowSize(int windowSize)
        {
            _config.WindowSize = windowSize;
            return this;
        }

        /// <summary>Sets the filter priority.</summary>
        public Builder WithPriority(int priority)
        {
            _config.Priority = priority;
            return this;
        }

        /// <summary>Sets the post filters.</summary>
        public Builder WithPostFilters(Policy.PostFilters? postFilters)
        {
            _config.PostFilters = postFilters;
            return this;
        }

        /// <summary>Sets the per-pattern regex match budget in milliseconds.</summary>
        public Builder WithRegexTimeoutMs(long regexTimeoutMs)
        {
            _config.RegexTimeoutMs = regexTimeoutMs;
            return this;
        }

        /// <summary>Builds and returns the <see cref="FilterConfiguration" />, validating it first.</summary>
        /// <exception cref="InvalidOperationException">If the configuration is invalid (e.g. a CRYPTO_REPLACE strategy with a missing or malformed encryption key).</exception>
        public FilterConfiguration Build()
        {
            Validate();
            return _config;
        }

        /// <summary>
        ///     Validates the configuration, failing fast at configuration time rather than per-document at
        ///     encryption time. Mirrors the Java reference: a CRYPTO_REPLACE strategy requires a crypto key
        ///     that is decodable hexadecimal of a legal AES length (16, 24, or 32 bytes). The key value
        ///     itself is never included in any error message.
        /// </summary>
        private void Validate()
        {
            if (_config.Strategies == null) return;

            foreach (var strategy in _config.Strategies)
            {
                if (string.Equals(strategy.Strategy, "FPE_ENCRYPT_REPLACE", StringComparison.OrdinalIgnoreCase))
                {
                    if (_config.Fpe == null)
                        throw new InvalidOperationException(
                            "Invalid configuration for filter: Missing FPE encryption property.");
                    if (string.IsNullOrEmpty(_config.Fpe.GetKey()))
                        throw new InvalidOperationException(
                            "Invalid configuration for filter: Missing FPE encryption key.");
                    if (string.IsNullOrEmpty(_config.Fpe.GetTweak()))
                        throw new InvalidOperationException(
                            "Invalid configuration for filter: Missing FPE encryption tweak value.");
                    continue;
                }

                if (!string.Equals(strategy.Strategy, "CRYPTO_REPLACE", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (_config.Crypto == null)
                    throw new InvalidOperationException(
                        "Invalid configuration for filter: Missing crypto encryption property.");

                var cryptoKey = _config.Crypto.GetKey();
                if (string.IsNullOrEmpty(cryptoKey))
                    throw new InvalidOperationException(
                        "Invalid configuration for filter: Missing crypto encryption key.");

                byte[] cryptoKeyBytes;
                try
                {
                    cryptoKeyBytes = Convert.FromHexString(cryptoKey);
                }
                catch (FormatException)
                {
                    throw new InvalidOperationException(
                        "Invalid configuration for filter: The crypto encryption key is not valid hexadecimal.");
                }

                if (cryptoKeyBytes.Length != 16 && cryptoKeyBytes.Length != 24 && cryptoKeyBytes.Length != 32)
                    throw new InvalidOperationException(
                        "Invalid configuration for filter: The crypto encryption key must be a 128-, 192-, "
                        + $"or 256-bit AES key (16, 24, or 32 bytes); got {cryptoKeyBytes.Length} bytes.");
            }
        }
    }
}