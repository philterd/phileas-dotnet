# phileas-net

phileas-net is a .NET library for detecting and filtering Personally Identifiable Information (PII) from text. It provides a flexible policy-driven approach to redacting, masking, replacing, or encrypting sensitive data such as SSNs, email addresses, phone numbers, credit card numbers, and more.

## Features

- **21 built-in PII identifiers** — SSN, email address, phone number, credit card, IP address, URL, date, street address, and more
- **Multiple filter strategies** — redact, mask, hash, encrypt (AES), random replace, static replace, and others
- **Policy-driven configuration** — define what to detect and how to replace it using plain C# objects or JSON
- **Referential integrity** — optional context service keeps random replacements consistent across documents
- **Extensible** — implement `IContextService` to persist replacement mappings in any store (Redis, database, etc.)

## Projects

The solution is split into four libraries:

| Project | Description |
|---|---|
| `Phileas.Model` | Core value types: `Span`, `FilterType`, `FilterPattern`, `Replacement` |
| `Phileas.Policy` | Policy configuration classes: `Policy`, `Identifiers`, filter strategy POCOs |
| `Phileas.Filters` | Abstract filter infrastructure: `AbstractFilter`, `FilterConfiguration`, `IContextService` |
| `Phileas.Services` | Concrete filter implementations and `FilterPolicyLoader` entry point |

## Quick Example

```csharp
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;

var policy = new Policy
{
    Name = "my-policy",
    Identifiers = new Identifiers
    {
        Ssn = new Ssn(),
        EmailAddress = new EmailAddress()
    }
};

var result = FilterPolicyLoader.Filter(policy, context: "default", piece: 0,
    input: "Patient SSN 123-45-6789, contact admin@example.com");

Console.WriteLine(result.FilteredText);
// Patient SSN {{{REDACTED-ssn}}}, contact {{{REDACTED-email-address}}}
```

## Next Steps

- [Getting Started](getting-started.md) — set up the library and run your first filter
- [Policies](policies.md) — understand how to configure policies
- [Supported Identifiers](supported-identifiers.md) — all 21 built-in PII types
- [Filter Strategies](filter-strategies.md) — control how detected PII is replaced
- [Context Service](context-service.md) — maintain referential integrity across documents
- [API Reference](api-reference.md) — detailed API documentation
