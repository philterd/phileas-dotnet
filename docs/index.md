# phileas-dotnet

phileas-dotnet is a .NET library for detecting and filtering Personally Identifiable Information (PII) from text. It provides a flexible policy-driven approach to redacting, masking, replacing, or encrypting sensitive data such as SSNs, email addresses, phone numbers, credit card numbers, and more.

## Features

- **A comprehensive set of built-in PII identifiers** — SSN, email, phone, credit card, IP, URL, date, ZIP, street address, names, locations, hospitals, custom dictionaries, custom regex identifiers, sections, and more
- **AI-powered entity detection** — PhEye filter backed by a remote NER service
- **Multiple filter strategies** — redact, mask, hash, encrypt (AES-GCM / FF3-1 format-preserving), realistic random replacement, static replacement, and others
- **Policy-driven configuration** — define what to detect and how to replace it using plain C# objects, JSON, or PhiSQL
- **Referential integrity** — opt into `CONTEXT` replacement scope to keep random replacements consistent across documents
- **Span disambiguation** — resolve competing classifications of the same text by surrounding context
- **PDF redaction** — detect and redact PII in PDFs, rasterizing pages so no text is recoverable
- **Extensible** — implement `IContextService` to persist replacement mappings in any store (Redis, database, etc.)

## Project

The `Phileas` library (NuGet package: `Phileas`) contains all filter types, policy configuration, and the `FilterService` entry point.

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

var result = new FilterService().Filter(policy, context: "default", piece: 0,
    input: "Patient SSN 123-45-6789, contact admin@example.com");

Console.WriteLine(result.FilteredText);
// Patient SSN {{{REDACTED-ssn}}}, contact {{{REDACTED-email-address}}}
```

## Next Steps

- [Getting Started](getting-started.md) — set up the library and run your first filter
- [Policies](policies.md) — understand how to configure policies
- [Supported Identifiers](supported-identifiers.md) — all built-in PII types plus the PhEye AI filter
- [PhEye Filter Usage](pheye-filter-usage.md) — AI-powered entity recognition via a remote PhEye service
- [Filter Strategies](filter-strategies.md) — control how detected PII is replaced
- [Filter Conditions](filter-conditions.md) — apply strategies conditionally based on context, confidence, population, or token
- [Context Service](context-service.md) — maintain referential integrity across documents
- [Span Disambiguation](span-disambiguation.md) — resolve competing classifications by context
- [PDF Redaction](pdf-redaction.md) — detect and redact PII in PDF documents
- [API Reference](api-reference.md) — detailed API documentation
