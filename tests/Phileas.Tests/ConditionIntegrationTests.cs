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
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;
using Phileas.Services;
using Xunit;

namespace Phileas.Tests;

public class ConditionIntegrationTests
{
    [Fact]
    public void EmailAddressFilter_WithConfidenceCondition_OnlyFiltersHighConfidence()
    {
        // Only redact emails with confidence > 0.5
        var policy = new Policy.Policy
        {
            Name = "conditional-policy",
            Identifiers = new Identifiers
            {
                EmailAddress = new EmailAddress
                {
                    Strategies = new List<EmailAddressFilterStrategy>
                    {
                        new()
                        {
                            Strategy = "REDACT",
                            Condition = "confidence > 0.5"
                        }
                    }
                }
            }
        };

        var result = new FilterService().Filter(policy, "ctx", 0, "Contact john@example.com for help");

        // Should be redacted because email has confidence 1.0
        Assert.Contains("REDACTED", result.FilteredText);
    }

    [Fact]
    public void SsnFilter_WithTokenCondition_OnlyRedactsSpecificPattern()
    {
        // Redact SSNs that start with "123", leave others as SAME
        var policy = new Policy.Policy
        {
            Name = "conditional-ssn",
            Identifiers = new Identifiers
            {
                Ssn = new Ssn
                {
                    Strategies = new List<SsnFilterStrategy>
                    {
                        new()
                        {
                            Strategy = "REDACT",
                            Condition = "token startswith \"123\""
                        },
                        new()
                        {
                            Strategy = "SAME" // Default: leave unchanged
                        }
                    }
                }
            }
        };

        var result1 = new FilterService().Filter(policy, "ctx", 0, "SSN: 123-45-6789");
        Assert.Contains("REDACTED", result1.FilteredText);

        var result2 = new FilterService().Filter(policy, "ctx", 0, "SSN: 987-65-4321");
        Assert.DoesNotContain("REDACTED", result2.FilteredText);
        Assert.Contains("987-65-4321", result2.FilteredText); // Should remain unchanged
    }

    [Fact]
    public void EmailFilter_WithContextCondition_FiltersOnlySpecificContext()
    {
        var policy = new Policy.Policy
        {
            Name = "context-specific",
            Identifiers = new Identifiers
            {
                EmailAddress = new EmailAddress
                {
                    Strategies = new List<EmailAddressFilterStrategy>
                    {
                        new()
                        {
                            Strategy = "MASK",
                            Condition = "context == \"sensitive-context\""
                        },
                        new()
                        {
                            Strategy = "SAME" // Default: leave unchanged
                        }
                    }
                }
            }
        };

        var result1 = new FilterService().Filter(policy, "sensitive-context", 0, "Email: test@example.com");
        Assert.Contains("*", result1.FilteredText);
        Assert.DoesNotContain("test@example.com", result1.FilteredText);

        var result2 = new FilterService().Filter(policy, "public-context", 0, "Email: test@example.com");
        Assert.DoesNotContain("*", result2.FilteredText);
        Assert.Contains("test@example.com", result2.FilteredText);
    }

    [Fact]
    public void MultipleStrategies_WithDifferentConditions_AppliesFirstMatch()
    {
        // Apply different strategies based on confidence
        var policy = new Policy.Policy
        {
            Name = "multi-strategy",
            Identifiers = new Identifiers
            {
                PhoneNumber = new PhoneNumber
                {
                    Strategies = new List<PhoneNumberFilterStrategy>
                    {
                        // Very high confidence (>=0.99): full redaction
                        new()
                        {
                            Strategy = "REDACT",
                            Condition = "confidence >= 0.99"
                        },
                        // High confidence (>=0.8): mask
                        new()
                        {
                            Strategy = "MASK",
                            Condition = "confidence >= 0.8"
                        },
                        // Low confidence: last 4
                        new()
                        {
                            Strategy = "LAST_4"
                        }
                    }
                }
            }
        };

        // A cleanly NANP-formatted number scores 0.95, so the first strategy (>=0.99) is skipped and the
        // second (MASK, >=0.8) is applied, confirming the first *matching* condition wins.
        var result = new FilterService().Filter(policy, "ctx", 0, "Call 555-123-4567 today");

        Assert.Contains("*", result.FilteredText);
        Assert.DoesNotContain("555-123-4567", result.FilteredText);
        Assert.Single(result.Spans);
        Assert.Equal("555-123-4567", result.Spans[0].Text);
    }

    [Fact]
    public void CombinedConditions_WithAnd_EvaluatesCorrectly()
    {
        var policy = new Policy.Policy
        {
            Name = "combined-conditions",
            Identifiers = new Identifiers
            {
                ZipCode = new ZipCode
                {
                    Strategies = new List<ZipCodeFilterStrategy>
                    {
                        new()
                        {
                            Strategy = "STATIC_REPLACE",
                            StaticReplacement = "00000",
                            Condition = "context == \"medical\"" // Simplified condition
                        },
                        new()
                        {
                            Strategy = "SAME" // Default: leave unchanged
                        }
                    }
                }
            }
        };

        // Context matches
        var result1 = new FilterService().Filter(policy, "medical", 0, "ZIP: 12345");
        Assert.Contains("00000", result1.FilteredText);
        Assert.DoesNotContain("12345", result1.FilteredText);

        // Context doesn't match
        var result2 = new FilterService().Filter(policy, "public", 0, "ZIP: 12345");
        Assert.DoesNotContain("00000", result2.FilteredText);
        Assert.Contains("12345", result2.FilteredText);
    }

    [Fact]
    public void NoCondition_AlwaysAppliesStrategy()
    {
        var policy = new Policy.Policy
        {
            Name = "no-condition",
            Identifiers = new Identifiers
            {
                EmailAddress = new EmailAddress()
            }
        };

        var result = new FilterService().Filter(policy, "ctx", 0, "Email: test@example.com");

        Assert.Contains("REDACTED", result.FilteredText);
        Assert.DoesNotContain("test@example.com", result.FilteredText);
    }

    [Fact]
    public void InvalidCondition_DefaultsToTrue_AppliesStrategy()
    {
        var policy = new Policy.Policy
        {
            Name = "invalid-condition",
            Identifiers = new Identifiers
            {
                EmailAddress = new EmailAddress
                {
                    Strategies = new List<EmailAddressFilterStrategy>
                    {
                        new()
                        {
                            Strategy = "REDACT",
                            Condition = "this is not a valid condition"
                        }
                    }
                }
            }
        };

        // Invalid condition should default to true and apply the strategy
        var result = new FilterService().Filter(policy, "ctx", 0, "Email: test@example.com");
        Assert.Contains("REDACTED", result.FilteredText);
    }
}