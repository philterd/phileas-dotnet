# Phileas (.NET)

A .NET port of [Phileas (Java)](https://github.com/philterd/phileas) — a library to deidentify and redact PII, PHI, and other sensitive information from text.

* Check out the [documentation](https://philterd.github.io/phileas-net/) or details and code examples.
* Built by [Philterd](https://www.philterd.ai).
* Commercial support and consulting is available - [contact us](https://www.philterd.ai).

## Overview

Phileas (.NET) is a library for detecting and redacting Personally Identifiable Information (PII) from text. Define a *policy* that describes which types of sensitive data to find, choose how each type should be handled, and call a single method to get back redacted text.

Phileas analyzes text searching for sensitive information such as email addresses, phone numbers, SSNs, credit card numbers, and many other types of PII/PHI. When sensitive information is identified, Phileas can manipulate it in a variety of ways: the information can be redacted, masked, hashed, or replaced with a static value. The user defines how to handle each type of sensitive information through policies (YAML or JSON).

Other capabilities include referential integrity for redactions, conditional logic for redactions, and a CLI.

Phileas requires no external dependencies (e.g. no ChatGPT/etc.) and is intended to be lightweight and easy to use.

## Supported Filter Types

| Filter | Description |
|---|---|
| `Age` | Ages (e.g. *42 years old*) |
| `BankRoutingNumber` | US bank routing numbers |
| `BitcoinAddress` | Bitcoin wallet addresses |
| `CreditCard` | Credit / debit card numbers |
| `Currency` | Currency amounts |
| `Date` | Dates in common formats |
| `DriversLicense` | US driver's license numbers |
| `EmailAddress` | Email addresses |
| `IbanCode` | IBAN bank account codes |
| `IpAddress` | IPv4 / IPv6 addresses |
| `MacAddress` | MAC (hardware) addresses |
| `PassportNumber` | Passport numbers |
| `PhoneNumber` | Phone numbers |
| `PhoneNumberExtension` | Phone number extensions |
| `Ssn` | US Social Security numbers |
| `StateAbbreviation` | US state abbreviations |
| `StreetAddress` | Street addresses |
| `TrackingNumber` | Parcel tracking numbers |
| `Url` | URLs |
| `Vin` | Vehicle Identification Numbers |
| `ZipCode` | US ZIP / ZIP+4 codes |

## Installation

Add the NuGet package to your project:

```shell
dotnet add package Phileas
```

## Quick Start

### Redact an email address

```csharp
using Phileas.Policy;
using Phileas.Policy.Filters;
using Phileas.Services;
using PhileasPolicy = Phileas.Policy.Policy;

var policy = new PhileasPolicy
{
    Name = "my-policy",
    Identifiers = new Identifiers
    {
        EmailAddress = new EmailAddress()
    }
};

var result = new FilterService().Filter(
    policy: policy,
    context: "default",
    piece: 0,
    input: "Contact john.doe@example.com for help."
);

Console.WriteLine(result.FilteredText);
// Output: Contact {{{REDACTED-email-address}}} for help.
```

### Redact multiple PII types at once

```csharp
var policy = new PhileasPolicy
{
    Name = "full-redaction",
    Identifiers = new Identifiers
    {
        EmailAddress = new EmailAddress(),
        PhoneNumber  = new PhoneNumber(),
        Ssn          = new Ssn(),
        CreditCard   = new CreditCard(),
        ZipCode      = new ZipCode()
    }
};

var result = new FilterService().Filter(
    policy: policy,
    context: "default",
    piece: 0,
    input: "Call 555-867-5309 or email jane@example.com. SSN: 123-45-6789."
);

Console.WriteLine(result.FilteredText);
```

### Inspect detected spans

Every result includes a list of `Span` objects that describe what was found:

```csharp
foreach (var span in result.Spans)
{
    Console.WriteLine($"[{span.FilterType}] '{span.Text}' at {span.CharacterStart}-{span.CharacterEnd} → '{span.Replacement}'");
}
```

## Filter Strategies

The default strategy is **REDACT**, which replaces PII with a formatted token such as `{{{REDACTED-email-address}}}`. You can customise the strategy on each filter:

```csharp
using Phileas.Filters;
using Phileas.Policy.Filters.Strategies;

// Mask the last 4 characters and hide the rest with '*'
var ssnStrategy = new SsnFilterStrategy
{
    Strategy = AbstractFilterStrategy.Last4
};

var policy = new PhileasPolicy
{
    Name = "mask-policy",
    Identifiers = new Identifiers
    {
        Ssn = new Ssn { Strategies = new List<SsnFilterStrategy> { ssnStrategy } }
    }
};
```

### Redaction Strategies

| C# constant | Strategy value | Behaviour |
|---|---|---|
| `AbstractFilterStrategy.Redact` | `REDACT` | Replace with `{{{REDACTED-%t}}}` (default) |
| `AbstractFilterStrategy.StaticReplace` | `STATIC_REPLACE` | Replace with a fixed string |
| `AbstractFilterStrategy.RandomReplace` | `RANDOM_REPLACE` | Replace with a random GUID (repeatable via Context Service) |
| `AbstractFilterStrategy.Mask` | `MASK` | Replace with a mask character (default `*`) |
| `AbstractFilterStrategy.Last4` | `LAST_4` | Keep the last 4 characters, mask the rest |
| `AbstractFilterStrategy.HashSha256Replace` | `HASH_SHA256_REPLACE` | Replace with the SHA-256 hash of the original value |
| `AbstractFilterStrategy.CryptoReplace` | `CRYPTO_REPLACE` | Encrypt the value |
| `AbstractFilterStrategy.FpeEncryptReplace` | `FPE_ENCRYPT_REPLACE` | Format-preserving encryption |

### Static replacement example

```csharp
var emailStrategy = new EmailAddressFilterStrategy
{
    Strategy        = AbstractFilterStrategy.StaticReplace,
    StaticReplacement = "user@redacted.invalid"
};
```

## Context Service and Referential Integrity

When using `RANDOM_REPLACE`, the Context Service ensures the same PII token always maps to the same replacement value within a named *context*. This preserves referential integrity across documents that reference the same identity.

```csharp
// FilterService uses InMemoryContextService automatically,
// but you can supply your own implementation.
IContextService contextService = new InMemoryContextService();

var result = new FilterService().Filter(
    policy: policy,
    context: "patient-123",   // all calls sharing this name reuse the same mappings
    piece: 0,
    input: "SSN: 123-45-6789",
    contextService: contextService
);
```

To share replacement values across processes or persist them between runs, implement `IContextService`:

```csharp
public class RedisContextService : IContextService
{
    private readonly IDatabase _db;

    public RedisContextService(IDatabase redisDatabase) => _db = redisDatabase;

    public string? Get(string contextName, string token)
    {
        var value = _db.HashGet(contextName, token);
        return value.IsNull ? null : (string?)value;
    }

    public void Put(string contextName, string token, string replacement)
        => _db.HashSet(contextName, token, replacement);
}
```

See [docs/context-service.md](docs/context-service.md) for a full walkthrough.

## Building and Testing

```shell
dotnet build Phileas.slnx
dotnet test  Phileas.slnx
```

## License

Copyright 2026 Philterd, LLC.

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.

"Phileas" and "Philter" are registered trademarks of Philterd, LLC.

This project is a .NET port of [Phileas](https://github.com/philterd/phileas), which is also Apache-2.0 licensed.