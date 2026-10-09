# phileas-dotnet

phileas-dotnet is a .NET library for detecting and filtering Personally Identifiable Information (PII) from text. It provides a flexible policy-driven approach to redacting, masking, replacing, or encrypting sensitive data such as SSNs, email addresses, phone numbers, credit card numbers, and more.

## Features

- A comprehensive set of **built-in PII identifiers** covering SSN, ITIN, Canadian SIN, email, phone, credit card, IP, URL, date, ZIP, street address, names, locations, hospitals, custom dictionaries, custom regex identifiers, sections, and more
- **AI-powered entity detection** through the PhEye filter, backed by a remote NER service
- **Multiple filter strategies**, including redact, mask, hash, encrypt (AES-GCM / FF3-1 format-preserving), realistic random replacement, static replacement, and others
- **Policy-driven configuration** that defines what to detect and how to replace it using plain C# objects, JSON, or PhiSQL
- **Referential integrity** through the opt-in `CONTEXT` replacement scope, which keeps random replacements consistent across documents
- **Span disambiguation** that resolves competing classifications of the same text by surrounding context
- **PDF redaction** that detects and redacts PII in PDFs, rasterizing pages so no text is recoverable
- **Word & Excel redaction** that detects and redacts PII in `.docx`/`.xlsx` documents in place (.NET port only)
- An optional cross-platform **REST service** (`Phileas.Rest`) for filtering text and documents, with MongoDB-backed policy/context management and OCR
- **Extensibility** through `IContextService`, which lets you persist replacement mappings in any store (Redis, database, etc.)

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

- [Getting Started](getting-started.md) walks through setting up the library and running your first filter
- [Policies](policies.md) explains how to configure policies
- [Supported Identifiers](supported-identifiers.md) covers all built-in PII types plus the PhEye AI filter
- [PhEye Filter Usage](pheye-filter-usage.md) covers AI-powered entity recognition via a remote PhEye service
- [Filter Strategies](filter-strategies.md) covers controlling how detected PII is replaced
- [Filter Conditions](filter-conditions.md) covers applying strategies conditionally based on context, confidence, population, or token
- [Context Service](context-service.md) covers maintaining referential integrity across documents
- [Span Disambiguation](span-disambiguation.md) covers resolving competing classifications by context
- [PDF Redaction](pdf-redaction.md) covers detecting and redacting PII in PDF documents
- [Word & Excel Redaction](office-redaction.md) covers detecting and redacting PII in `.docx`/`.xlsx` documents
- [REST Service](rest-service.md) covers running phileas-dotnet as an HTTP service for text and document redaction
- [API Reference](api-reference.md) has the detailed API documentation
