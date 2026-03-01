# Getting Started

This guide walks you through adding phileas-net to a .NET project and running your first PII filter.

## Prerequisites

- .NET 8 or later
- A project that references `Phileas.Services`

## Installation

Add a project reference or NuGet package reference to `Phileas.Services`:

```xml
<ProjectReference Include="../src/Phileas.Services/Phileas.Services.csproj" />
```

## Basic Usage

### 1. Define a Policy

A [`Policy`](api-reference.md#policy) describes which PII types to detect and how to handle them.

```csharp
using Phileas.Policy;
using Phileas.Policy.Filters;

var policy = new Policy
{
    Name = "basic-policy",
    Identifiers = new Identifiers
    {
        Ssn = new Ssn(),
        EmailAddress = new EmailAddress(),
        PhoneNumber = new PhoneNumber()
    }
};
```

By default, all detected PII is **redacted** (replaced with `{{{REDACTED-<type>}}}`).

### 2. Filter Text

Call `FilterService.Filter` with the policy, a context name, a piece index, and the input text:

```csharp
using Phileas.Services;

var result = FilterService.Filter(
    policy,
    context: "session-1",
    piece: 0,
    input: "SSN: 123-45-6789  Email: alice@example.com  Phone: 555-867-5309"
);

Console.WriteLine(result.FilteredText);
// SSN: {{{REDACTED-ssn}}}  Email: {{{REDACTED-email-address}}}  Phone: {{{REDACTED-phone-number}}}
```

### 3. Inspect the Results

`FilterService.Filter` returns a [`TextFilterResult`](api-reference.md#textfilterresult) that contains the filtered text and a list of [`Span`](api-reference.md#span) objects describing each detected PII occurrence:

```csharp
foreach (var span in result.Spans)
{
    Console.WriteLine($"[{span.CharacterStart}–{span.CharacterEnd}] {span.FilterType}: \"{span.Text}\" → \"{span.Replacement}\"");
}
```

## Customising the Redaction Format

Each filter type supports a custom `redactionFormat`. Use `%t` as a placeholder for the filter type name:

```csharp
using Phileas.Policy.Filters;
using Phileas.Policy.Filters.Strategies;

var policy = new Policy
{
    Name = "custom-format",
    Identifiers = new Identifiers
    {
        Ssn = new Ssn
        {
            Strategies = new List<SsnFilterStrategy>
            {
                new SsnFilterStrategy
                {
                    Strategy = "REDACT",
                    RedactionFormat = "[REMOVED-%t]"
                }
            }
        }
    }
};
```

## Loading a Policy from JSON

Policies can be serialised as JSON and deserialised at runtime:

```csharp
using System.Text.Json;
using Phileas.Policy;

const string json = """
{
  "name": "json-policy",
  "identifiers": {
    "ssn": {},
    "emailAddress": {}
  }
}
""";

var policy = JsonSerializer.Deserialize<Policy>(json)!;
```

## Using Conditional Strategies

Apply different strategies based on context, confidence, or the detected token value:

```csharp
using Phileas.Policy.Filters.Strategies;

var policy = new Policy
{
    Name = "conditional-policy",
    Identifiers = new Identifiers
    {
        EmailAddress = new EmailAddress
        {
            Strategies = new List<EmailAddressFilterStrategy>
            {
                // Fully redact emails in medical context
                new EmailAddressFilterStrategy
                {
                    Strategy = "REDACT",
                    Condition = "context == \"medical\""
                },
                // Mask emails in other contexts
                new EmailAddressFilterStrategy
                {
                    Strategy = "MASK"
                }
            }
        }
    }
};

// Medical context - email will be redacted
var medicalResult = FilterService.Filter(policy, "medical", 0, "Contact: john@example.com");
// Output: "Contact: {{{REDACTED-email-address}}}"

// Other context - email will be masked
var defaultResult = FilterService.Filter(policy, "default", 0, "Contact: john@example.com");
// Output: "Contact: ****************"
```

See [Filter Conditions](filter-conditions.md) for detailed examples and advanced usage.

## Running the Tests

```bash
dotnet test tests/Phileas.Tests/Phileas.Tests.csproj
```

## Next Steps

- [Policies](policies.md) — configure policy options such as window size and ignored values
- [Supported Identifiers](supported-identifiers.md) — full list of built-in PII types
- [Filter Strategies](filter-strategies.md) — choose how PII is replaced
- [Filter Conditions](filter-conditions.md) — apply strategies conditionally
- [Context Service](context-service.md) — keep random replacements consistent across calls
