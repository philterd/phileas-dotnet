# Filter Strategy Conditions

Filter strategy conditions allow you to apply different strategies based on the detected token's properties. Each strategy in a filter's `Strategies` list can include a `condition` property that determines when that strategy should be applied.

## Overview

When multiple strategies are defined for a filter, phileas-dotnet evaluates their conditions in order. The **first strategy whose condition evaluates to `true`** is applied. If no strategy conditions match, the default redaction format is used.

## Supported Condition Fields

| Field | Type | Description | Example |
|---|---|---|---|
| `confidence` | `double` | The detection confidence score (0.0 to 1.0) | `confidence > 0.9` |
| `context` | `string` | The context name passed to `FilterService.Filter()` | `context == "medical"` |
| `token` | `string` | The detected text value | `token startswith "555"` |
| `type` | `string` | The classification type (e.g., `PER`, `LOC`) | `type == "PER"` |
| `population` | `number` | Census population of the detected ZIP code (looks the token up in the bundled census data) | `population > 50000` |

## Supported Operators

### Comparison Operators

| Operator | Description | Example |
|---|---|---|
| `==` or `is` | Equals | `confidence == 0.95` |
| `!=` or `is not` | Not equals | `context is not "public"` |
| `>` | Greater than | `confidence > 0.8` |
| `<` | Less than | `confidence < 0.5` |
| `>=` | Greater than or equal | `confidence >= 0.9` |
| `<=` | Less than or equal | `confidence <= 0.7` |

### String Operators

| Operator | Description | Example |
|---|---|---|
| `startswith` | String starts with | `token startswith "123"` |

### Logical Operators

| Operator | Description | Example |
|---|---|---|
| `and` | Logical AND | `confidence > 0.8 and context == "medical"` |

**Note:** Multiple conditions can be combined with `and`. All conditions must be true for the strategy to apply.

## Basic Examples

### Confidence-Based Filtering

Apply different strategies based on detection confidence:

```csharp
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;

var policy = new Policy
{
    Name = "confidence-policy",
    Identifiers = new Identifiers
    {
        PhoneNumber = new PhoneNumber
        {
            Strategies = new List<PhoneNumberFilterStrategy>
            {
                // High confidence: full redaction
                new PhoneNumberFilterStrategy
                {
                    Strategy = "REDACT",
                    Condition = "confidence >= 0.95"
                },
                // Medium confidence: mask
                new PhoneNumberFilterStrategy
                {
                    Strategy = "MASK",
                    Condition = "confidence >= 0.8"
                },
                // Low confidence: keep last 4 digits
                new PhoneNumberFilterStrategy
                {
                    Strategy = "LAST_4"
                }
            }
        }
    }
};

var result = new FilterService().Filter(policy, "context", 0, "Call 555-867-5309");
```

### Context-Based Filtering

Apply different strategies depending on the context:

```csharp
var policy = new Policy
{
    Name = "context-policy",
    Identifiers = new Identifiers
    {
        EmailAddress = new EmailAddress
        {
            Strategies = new List<EmailAddressFilterStrategy>
            {
                // Fully redact in medical context
                new EmailAddressFilterStrategy
                {
                    Strategy = "REDACT",
                    Condition = "context == \"medical\""
                },
                // Mask in internal context
                new EmailAddressFilterStrategy
                {
                    Strategy = "MASK",
                    Condition = "context == \"internal\""
                },
                // Leave unchanged in public context
                new EmailAddressFilterStrategy
                {
                    Strategy = "SAME"
                }
            }
        }
    }
};

// Medical context - email will be redacted
var result1 = new FilterService().Filter(policy, "medical", 0, "Contact: john@example.com");
// Output: "Contact: {{{REDACTED-email-address}}}"

// Public context - email unchanged
var result2 = new FilterService().Filter(policy, "public", 0, "Contact: john@example.com");
// Output: "Contact: john@example.com"
```

### Token-Based Filtering

Apply strategies based on the token's value:

```csharp
var policy = new Policy
{
    Name = "token-policy",
    Identifiers = new Identifiers
    {
        Ssn = new Ssn
        {
            Strategies = new List<SsnFilterStrategy>
            {
                // Redact SSNs starting with 123
                new SsnFilterStrategy
                {
                    Strategy = "REDACT",
                    Condition = "token startswith \"123\""
                },
                // Hash all other SSNs
                new SsnFilterStrategy
                {
                    Strategy = "HASH_SHA256_REPLACE"
                }
            }
        }
    }
};

var result1 = new FilterService().Filter(policy, "ctx", 0, "SSN: 123-45-6789");
// Output: "SSN: {{{REDACTED-ssn}}}"

var result2 = new FilterService().Filter(policy, "ctx", 0, "SSN: 987-65-4321");
// Output: "SSN: a1b2c3d4..." (SHA-256 hash)
```

## Advanced Examples

### Combined Conditions

Use multiple conditions with `and`:

```csharp
var policy = new Policy
{
    Name = "combined-policy",
    Identifiers = new Identifiers
    {
        ZipCode = new ZipCode
        {
            Strategies = new List<ZipCodeFilterStrategy>
            {
                // Replace with 00000 for high-confidence medical data
                new ZipCodeFilterStrategy
                {
                    Strategy = "STATIC_REPLACE",
                    StaticReplacement = "00000",
                    Condition = "confidence >= 0.9 and context == \"medical\""
                },
                // Mask for medium confidence
                new ZipCodeFilterStrategy
                {
                    Strategy = "MASK",
                    Condition = "confidence >= 0.7"
                },
                // Leave others unchanged
                new ZipCodeFilterStrategy
                {
                    Strategy = "SAME"
                }
            }
        }
    }
};
```

### Type-Based Filtering

Filter based on classification type (useful for named entity recognition):

```csharp
var policy = new Policy
{
    Name = "type-policy",
    Identifiers = new Identifiers
    {
        PhEyes = new List<PhEye>
        {
            new PhEye
            {
                Strategies = new List<PhEyeFilterStrategy>
                {
                    // Replace person names with consistent pseudonyms
                    new PhEyeFilterStrategy
                    {
                        Strategy = "RANDOM_REPLACE",
                        Condition = "type == \"PER\""
                    },
                    // Redact locations
                    new PhEyeFilterStrategy
                    {
                        Strategy = "REDACT",
                        Condition = "type == \"LOC\""
                    },
                    // Leave other types unchanged
                    new PhEyeFilterStrategy
                    {
                        Strategy = "SAME"
                    }
                }
            }
        }
    }
};
```

### Multi-Level Security

Different strategies for different security levels:

```csharp
var policy = new Policy
{
    Name = "security-levels",
    Identifiers = new Identifiers
    {
        CreditCard = new CreditCard
        {
            Strategies = new List<CreditCardFilterStrategy>
            {
                // Top secret: hash everything
                new CreditCardFilterStrategy
                {
                    Strategy = "HASH_SHA256_REPLACE",
                    Condition = "context == \"top-secret\""
                },
                // Confidential: show last 4
                new CreditCardFilterStrategy
                {
                    Strategy = "LAST_4",
                    Condition = "context == \"confidential\""
                },
                // Internal: mask
                new CreditCardFilterStrategy
                {
                    Strategy = "MASK",
                    Condition = "context == \"internal\""
                },
                // Public: full redaction
                new CreditCardFilterStrategy
                {
                    Strategy = "REDACT"
                }
            }
        }
    }
};
```

## JSON Configuration

Conditions can also be specified in JSON policy files:

```json
{
  "name": "json-conditions",
  "identifiers": {
    "emailAddress": {
      "emailAddressFilterStrategies": [
        {
          "strategy": "MASK",
          "condition": "confidence > 0.8 and context == \"internal\""
        },
        {
          "strategy": "SAME"
        }
      ]
    }
  }
}
```

## Condition Evaluation Rules

1. **Order Matters**: Strategies are evaluated in the order they appear in the list. The first matching condition wins.

2. **Default Strategy**: Always include a final strategy with no condition to serve as a fallback. Without this, unmatched tokens will use the default redaction format.

3. **Case Insensitivity**: Field names and operators are case-insensitive (`CONFIDENCE`, `confidence`, and `Confidence` all work).

4. **String Quoting**: String values must be enclosed in double quotes: `context == "medical"`.

5. **Invalid Conditions**: If a condition cannot be parsed, it defaults to `true` and the strategy is applied.

## Common Patterns

### Progressive Disclosure

Show more detail as confidence decreases:

```csharp
Strategies = new List<SsnFilterStrategy>
{
    new SsnFilterStrategy { Strategy = "REDACT", Condition = "confidence >= 0.95" },
    new SsnFilterStrategy { Strategy = "LAST_4", Condition = "confidence >= 0.8" },
    new SsnFilterStrategy { Strategy = "SAME" }
}
```

### Context-Specific Handling

Different behavior for different contexts:

```csharp
Strategies = new List<PhoneNumberFilterStrategy>
{
    new PhoneNumberFilterStrategy { Strategy = "REDACT", Condition = "context == \"public\"" },
    new PhoneNumberFilterStrategy { Strategy = "HASH_SHA256_REPLACE", Condition = "context == \"audit\"" },
    new PhoneNumberFilterStrategy { Strategy = "SAME", Condition = "context == \"internal\"" }
}
```

### Token Pattern Matching

Special handling for specific patterns:

```csharp
Strategies = new List<IpAddressFilterStrategy>
{
    new IpAddressFilterStrategy { Strategy = "SAME", Condition = "token startswith \"192.168\"" },
    new IpAddressFilterStrategy { Strategy = "SAME", Condition = "token startswith \"10.\"" },
    new IpAddressFilterStrategy { Strategy = "HASH_SHA256_REPLACE" }
}
```

## Population Conditions

The `population` field applies to **ZIP code** tokens. phileas-dotnet looks the detected ZIP code up in its bundled US census population data and compares the result against the configured value. A ZIP code that is not present in the census data **fails** the condition.

```csharp
var policy = new Policy
{
    Name = "population-policy",
    Identifiers = new Identifiers
    {
        ZipCode = new ZipCode
        {
            Strategies = new List<ZipCodeFilterStrategy>
            {
                // Only redact ZIP codes for places with a small population (more identifying).
                new ZipCodeFilterStrategy
                {
                    Strategy = "REDACT",
                    Condition = "population < 20000"
                },
                // Leave large-population ZIP codes unchanged.
                new ZipCodeFilterStrategy { Strategy = "SAME" }
            }
        }
    }
};
```

## Limitations

- **OR Operator**: Only `and` is supported for combining conditions. Use multiple strategies for OR logic.
- **Complex Expressions**: Parentheses and nested conditions are not supported.
- **Regular Expressions**: The `token` field supports `startswith` but not full regex matching.

## See Also

- [Filter Strategies](filter-strategies.md) - Available strategy types
- [Policies](policies.md) - Policy configuration
- [API Reference](api-reference.md) - FilterService methods
